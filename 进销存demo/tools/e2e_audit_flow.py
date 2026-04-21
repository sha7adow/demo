# -*- coding: utf-8 -*-
"""
端到端：Admin 建商品 → 采购入库/退货 → 盘点 → 销售出库/退货；
并用 SQLite 校验 AuditLogs / StockTransactions。
依赖：dotnet build、Python 3。
"""
from __future__ import annotations

import os
import re
import sqlite3
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from http.cookiejar import CookieJar


def token(html: str) -> str:
    m = re.search(
        r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"',
        html,
    )
    if not m:
        m = re.search(
            r'value="([^"]+)" type="hidden" name="__RequestVerificationToken"',
            html,
        )
    if not m:
        raise RuntimeError("无法在 HTML 中找到 __RequestVerificationToken")
    return m.group(1)


class Client:
    def __init__(self, base: str):
        self.base = base.rstrip("/")
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(CookieJar())
        )

    def get(self, path: str) -> str:
        req = urllib.request.Request(self.base + path)
        with self.opener.open(req, timeout=120) as resp:
            return resp.read().decode("utf-8", errors="replace")

    def post_form(self, path: str, data: dict[str, str]) -> None:
        body = urllib.parse.urlencode(data, doseq=True).encode("utf-8")
        req = urllib.request.Request(
            self.base + path, data=body, method="POST"
        )
        req.add_header(
            "Content-Type", "application/x-www-form-urlencoded; charset=UTF-8"
        )
        with self.opener.open(req, timeout=120):
            pass

    def post_form_pairs(self, path: str, pairs: list[tuple[str, str]]) -> None:
        body = urllib.parse.urlencode(pairs, doseq=True).encode("utf-8")
        req = urllib.request.Request(
            self.base + path, data=body, method="POST"
        )
        req.add_header(
            "Content-Type", "application/x-www-form-urlencoded; charset=UTF-8"
        )
        with self.opener.open(req, timeout=120):
            pass


def wait_server(c: Client, timeout: float = 90) -> None:
    t0 = time.time()
    while time.time() - t0 < timeout:
        try:
            c.get("/Account/Login")
            return
        except urllib.error.URLError:
            time.sleep(0.4)
    raise TimeoutError("服务未在超时内启动")


def sql_one(db_path: str, q: str, args=()) -> object | None:
    conn = sqlite3.connect(db_path)
    try:
        cur = conn.execute(q, args)
        row = cur.fetchone()
        return row[0] if row else None
    finally:
        conn.close()


def assert_true(name: str, ok: bool, detail: str = "") -> None:
    msg = f"[{'OK' if ok else 'FAIL'}] {name}"
    if detail:
        msg += f" — {detail}"
    print(msg)
    if not ok:
        raise AssertionError(name)


def main() -> int:
    project_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    csproj = os.path.join(project_dir, "进销存demo.csproj")
    if not os.path.isfile(csproj):
        print("找不到项目文件:", csproj, file=sys.stderr)
        return 2

    db_path = os.path.join(project_dir, "jxc-e2e-audit.db")
    for suf in ("", "-shm", "-wal"):
        p = db_path + suf if suf else db_path
        if os.path.isfile(p):
            try:
                os.remove(p)
            except OSError:
                pass

    url = "http://127.0.0.1:5017"
    env = os.environ.copy()
    env["ConnectionStrings__Default"] = f"Data Source={db_path}"
    env["ASPNETCORE_ENVIRONMENT"] = "Development"

    subprocess.run(
        ["dotnet", "build", "-c", "Release", "-v", "q"],
        cwd=project_dir,
        check=True,
    )
    proc = subprocess.Popen(
        ["dotnet", "run", "-c", "Release", "--no-build", "--urls", url],
        cwd=project_dir,
        env=env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
    )
    c = Client(url)
    try:
        wait_server(c, 90)

        # ---- admin：登录 + 新建 P999 ----
        h = c.get("/Account/Login")
        c.post_form(
            "/Account/Login",
            {
                "__RequestVerificationToken": token(h),
                "UserName": "admin",
                "Password": "Jxc@123456",
                "RememberMe": "false",
            },
        )
        category_id = str(
            sql_one(
                db_path,
                "SELECT Id FROM ProductCategories WHERE Name = '饮料' LIMIT 1",
            )
        )
        assert_true("种子分类「饮料」存在", category_id and category_id != "None")

        h = c.get("/Products/Create")
        c.post_form(
            "/Products/Create",
            {
                "__RequestVerificationToken": token(h),
                "Code": "P999",
                "Name": "P999测试商品",
                "Unit": "个",
                "SafetyStock": "5",
                "Barcode": "",
                "CategoryId": category_id,
                "PurchasePrice": "10",
                "SalePrice": "12",
                "Stock": "10",
                "IsActive": "true",
                "Remark": "e2e",
            },
        )

        pid = int(sql_one(db_path, "SELECT Id FROM Products WHERE Code = ?", ("P999",)))
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        try:
            row = conn.execute(
                "SELECT UserName, EntityType, Action, Changes FROM AuditLogs "
                "WHERE EntityType = 'Product' AND EntityKey = ? ORDER BY Id DESC LIMIT 1",
                (str(pid),),
            ).fetchone()
        finally:
            conn.close()

        assert_true("AuditLog 有 Product 记录", row is not None)
        assert_true("AuditLog UserName=admin", row["UserName"] == "admin")
        assert_true("AuditLog Action=Insert(1)", int(row["Action"]) == 1)
        assert_true(
            "AuditLog Changes 为 JSON 且含 Code",
            row["Changes"] is not None and '"Code"' in row["Changes"],
            row["Changes"] or "",
        )

        # 注销 → purchaser
        h = c.get("/Home/Index")
        c.post_form(
            "/Account/Logout", {"__RequestVerificationToken": token(h)}
        )
        h = c.get("/Account/Login")
        c.post_form(
            "/Account/Login",
            {
                "__RequestVerificationToken": token(h),
                "UserName": "purchaser",
                "Password": "Jxc@123456",
                "RememberMe": "false",
            },
        )

        h = c.get("/Purchases/Create")
        m_on = re.search(
            r'<input[^>]*name="OrderNo"[^>]*value="([^"]*)"', h, re.IGNORECASE
        )
        assert_true("解析采购预览单号", m_on is not None)
        order_no_preview = m_on.group(1)
        sup_id = int(
            sql_one(db_path, "SELECT Id FROM Suppliers ORDER BY Id LIMIT 1")
        )
        today = time.strftime("%Y-%m-%d")
        c.post_form(
            "/Purchases/Create",
            {
                "__RequestVerificationToken": token(h),
                "OrderNo": order_no_preview,
                "OrderDate": today,
                "SupplierId": str(sup_id),
                "Remark": "e2e采购",
                "productIds": str(pid),
                "quantities": "5",
                "unitPrices": "10",
            },
        )

        po_id = int(
            sql_one(db_path, "SELECT Id FROM PurchaseOrders ORDER BY Id DESC LIMIT 1")
        )
        h = c.get(f"/Purchases/Details/{po_id}")
        c.post_form(
            f"/Purchases/Confirm/{po_id}",
            {"__RequestVerificationToken": token(h)},
        )

        rmk = sql_one(
            db_path,
            "SELECT Remark FROM StockTransactions WHERE ProductId = ? AND ChangeType = 1 ORDER BY Id DESC LIMIT 1",
            (pid,),
        )
        assert_true("流水含「采购入库」", rmk is not None and "采购入库" in str(rmk), str(rmk))

        n_po_upd = int(
            sql_one(
                db_path,
                "SELECT COUNT(*) FROM AuditLogs WHERE UserName = 'purchaser' AND EntityType = 'PurchaseOrder' AND Action = 2",
            )
        )
        n_pr_upd = int(
            sql_one(
                db_path,
                "SELECT COUNT(*) FROM AuditLogs WHERE UserName = 'purchaser' AND EntityType = 'Product' AND EntityKey = ? AND Action = 2",
                (str(pid),),
            )
        )
        assert_true("AuditLogs：PurchaseOrder Update（采购员）", n_po_upd >= 1, str(n_po_upd))
        assert_true("AuditLogs：Product Update 库存（采购员）", n_pr_upd >= 1, str(n_pr_upd))

        h = c.get(f"/Purchases/Details/{po_id}")
        c.post_form(
            f"/Purchases/Return/{po_id}",
            {"__RequestVerificationToken": token(h), "remark": ""},
        )
        st = int(
            sql_one(db_path, "SELECT Status FROM PurchaseOrders WHERE Id = ?", (po_id,))
        )
        assert_true("采购单状态=已退货(3)", st == 3)
        rmk2 = sql_one(
            db_path,
            "SELECT Remark FROM StockTransactions WHERE ProductId = ? AND ChangeType = 4 ORDER BY Id DESC LIMIT 1",
            (pid,),
        )
        assert_true("流水含「采购退货」", rmk2 is not None and "采购退货" in str(rmk2), str(rmk2))

        # warehouse：盘点
        h = c.get("/Home/Index")
        c.post_form(
            "/Account/Logout", {"__RequestVerificationToken": token(h)}
        )
        h = c.get("/Account/Login")
        c.post_form(
            "/Account/Login",
            {
                "__RequestVerificationToken": token(h),
                "UserName": "warehouse",
                "Password": "Jxc@123456",
                "RememberMe": "false",
            },
        )
        h = c.get("/Stocktakes")
        c.post_form(
            "/Stocktakes/Create",
            {"__RequestVerificationToken": token(h), "remark": "e2e盘点"},
        )
        st_id = int(
            sql_one(db_path, "SELECT Id FROM Stocktakes ORDER BY Id DESC LIMIT 1")
        )
        h = c.get(f"/Stocktakes/Details/{st_id}")
        rv = token(h)
        pairs: list[tuple[str, str]] = []
        for block in re.findall(r"<tr>(.*?)</tr>", h, re.DOTALL):
            if 'name="productIds"' not in block:
                continue
            mp = re.search(r'name="productIds" value="(\d+)"', block)
            ms = re.search(
                r'<td class="text-end">(\d+)</td>\s*<td class="text-end">\s*<input name="actualQtys"',
                block,
                re.DOTALL,
            )
            ma = re.search(r'name="actualQtys"[^>]*value="(\d+)"', block)
            if not (mp and ms and ma):
                continue
            p_id, sys_q, act_q = int(mp.group(1)), int(ms.group(1)), int(ma.group(1))
            if p_id == pid:
                act_q = sys_q - 3
            pairs.append((str(p_id), str(act_q)))

        assert_true("解析到盘点行", len(pairs) > 0)
        flat: list[tuple[str, str]] = [("__RequestVerificationToken", rv)]
        for p_id, aq in pairs:
            flat.append(("productIds", p_id))
            flat.append(("actualQtys", aq))
        c.post_form_pairs(f"/Stocktakes/Save/{st_id}", flat)

        h = c.get(f"/Stocktakes/Details/{st_id}")
        c.post_form(
            f"/Stocktakes/Confirm/{st_id}",
            {"__RequestVerificationToken": token(h)},
        )

        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        try:
            row_st = conn.execute(
                "SELECT Quantity, Remark FROM StockTransactions WHERE ProductId = ? AND ChangeType = 6 ORDER BY Id DESC LIMIT 1",
                (pid,),
            ).fetchone()
        finally:
            conn.close()
        assert_true("盘点流水 Quantity=-3", row_st is not None and int(row_st["Quantity"]) == -3)
        assert_true(
            "盘点流水 Remark 含「盘点」",
            row_st is not None and "盘点" in (row_st["Remark"] or ""),
            row_st["Remark"] if row_st else "",
        )
        n_st_audit = int(
            sql_one(
                db_path,
                "SELECT COUNT(*) FROM AuditLogs WHERE UserName = 'warehouse' AND EntityType = 'Stocktake' AND Action = 2",
            )
        )
        assert_true("AuditLogs：Stocktake Update（库管）", n_st_audit >= 1, str(n_st_audit))

        # salesperson：销售出库 + 退货
        h = c.get("/Home/Index")
        c.post_form(
            "/Account/Logout", {"__RequestVerificationToken": token(h)}
        )
        h = c.get("/Account/Login")
        c.post_form(
            "/Account/Login",
            {
                "__RequestVerificationToken": token(h),
                "UserName": "salesperson",
                "Password": "Jxc@123456",
                "RememberMe": "false",
            },
        )
        cust_id = int(
            sql_one(db_path, "SELECT Id FROM Customers ORDER BY Id LIMIT 1")
        )

        h = c.get("/Sales/Create")
        m_on = re.search(
            r'<input[^>]*name="OrderNo"[^>]*value="([^"]*)"', h, re.IGNORECASE
        )
        assert_true("解析销售预览单号", m_on is not None)
        so_preview = m_on.group(1)
        c.post_form(
            "/Sales/Create",
            {
                "__RequestVerificationToken": token(h),
                "OrderNo": so_preview,
                "OrderDate": today,
                "CustomerId": str(cust_id),
                "Remark": "e2e销售",
                "productIds": str(pid),
                "quantities": "2",
                "unitPrices": "12",
            },
        )
        so_id = int(
            sql_one(db_path, "SELECT Id FROM SaleOrders ORDER BY Id DESC LIMIT 1")
        )
        h = c.get(f"/Sales/Details/{so_id}")
        c.post_form(
            f"/Sales/Confirm/{so_id}",
            {"__RequestVerificationToken": token(h)},
        )
        rmk_s = sql_one(
            db_path,
            "SELECT Remark FROM StockTransactions WHERE ProductId = ? AND ChangeType = 2 ORDER BY Id DESC LIMIT 1",
            (pid,),
        )
        assert_true("流水含「销售出库」", rmk_s is not None and "销售出库" in str(rmk_s), str(rmk_s))

        h = c.get(f"/Sales/Details/{so_id}")
        c.post_form(
            f"/Sales/Return/{so_id}",
            {"__RequestVerificationToken": token(h), "remark": ""},
        )
        rmk_sr = sql_one(
            db_path,
            "SELECT Remark FROM StockTransactions WHERE ProductId = ? AND ChangeType = 5 ORDER BY Id DESC LIMIT 1",
            (pid,),
        )
        assert_true("流水含「销售退货」", rmk_sr is not None and "销售退货" in str(rmk_sr), str(rmk_sr))

        print("全部断言通过。")
        return 0
    except Exception as ex:
        print("失败:", ex, file=sys.stderr)
        if proc.stderr:
            err = proc.stderr.read()
            if err:
                print(err.decode("utf-8", errors="replace"), file=sys.stderr)
        return 1
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            proc.kill()


if __name__ == "__main__":
    raise SystemExit(main())
