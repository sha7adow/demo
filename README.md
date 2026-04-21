# 进销存 Demo（ASP.NET Core 8 + EF Core + SQLite）

这是一个用于练习 **ASP.NET Core MVC + EF Core** 的进销存小系统，覆盖商品、供应商/客户、采购/销售、库存流水、盘点、应收/应付、导入导出、审计日志与仪表盘等功能，并提供端到端脚本作为回归基线。

## 本地运行

- **要求**：.NET SDK（建议 8.x）、Python 3（仅用于端到端脚本）
- **启动**：

```bash
cd 进销存demo
dotnet run
```

首次启动会自动执行 `db.Database.Migrate()` 并写入种子数据（包含默认账号/角色）。

## 默认账号

密码默认在 `appsettings.json` 的 `Jxc:Seed:DefaultPassword`（默认 `Jxc@123456`）。

- `admin`：管理员
- `purchaser`：采购员
- `salesperson`：销售员
- `warehouse`：库管

## 端到端回归（e2e）

脚本：`进销存demo/tools/e2e_audit_flow.py`

它会走一条完整业务链路：

- Admin 建商品
- 采购入库/退货
- 盘点确认（生成调整流水）
- 销售出库/退货
- 用 SQLite 直接校验 `AuditLogs` / `StockTransactions` 关键数据

运行方式（先确保站点已启动并监听 `http://localhost:5253`）：

```bash
python 进销存demo/tools/e2e_audit_flow.py
```

脚本末尾输出 `全部流程通过` 即表示回归通过。

## Excel 导入/导出（ClosedXML）

服务：`进销存demo/Services/ExcelService.cs`

- **为什么用 ClosedXML**：
  - 不依赖 Office 安装，跨平台
  - API 直观、适合快速交付“可用的业务 Excel”
- **实现要点**：
  - `Export` 里尽量保留 **数值/日期类型**（而不是一律写字符串），这样 Excel 才能正常筛选、排序、汇总
  - `ColumnSpec<T>.Format` 用于设置列格式（如 `0.00`、日期格式等）
  - 首行冻结、标题加粗、开启 AutoFilter、列宽自适应（有限行范围内自适应，避免大表性能问题）

控制器侧通常只需要：

- 构造导出行（实体或 DTO）
- 定义列：`new ColumnSpec<T>("表头", x => x.Prop, "0.00")`
- `return File(bytes, "...xlsx", "xxx.xlsx")`

## 架构决策记录（为什么这么做）

### 1) 为什么用 EF Core SaveChanges 拦截器做审计（Audit）

文件：`进销存demo/Data/AuditSaveChangesInterceptor.cs`

目标：**让审计成为“基础设施能力”而不是到处散落的业务代码**。

拦截器集中处理：

- `IAuditable`：统一维护 `CreatedAt / UpdatedAt`
- `ISoftDelete`：把物理删除转换为软删（`IsDeleted=true` + `DeletedAt`）
- `Product.RowVersion`：每次修改自动递增
- `IAuditLogged`：写入 `AuditLogs`（Insert/Update/Delete，字段变更 JSON）

这样做的收益：

- Controller/Service 不需要重复写“保存前更新时间”“写审计日志”等样板代码
- 审计覆盖面更完整：只要走 `SaveChanges` 就能捕获
- e2e 脚本可以直接对审计表做数据库级断言，形成稳定回归基线

### 2) 为什么用 Options 模式（`IOptions<JxcOptions>`）

文件：`进销存demo/Models/Options/JxcOptions.cs`，注册在 `Program.cs`

目标：把“可调业务参数”从代码里抽出来，配置化并可集中管理。

典型场景：

- 分页默认值/上限（`Jxc:Paging`）
- 单号前缀（`Jxc:OrderPrefix`）
- Seed 默认密码（`Jxc:Seed`）
- PDF 字体等环境差异参数（`Jxc:Company`）

再结合 `PopulatePagingDefaultsFilter`（见下条），可以做到：

- 改 `appsettings.json` 即生效
- 避免每个 Controller 到处写默认 PageSize/封顶逻辑

### 3) 为什么把分页默认值下沉到 ActionFilter

文件：`进销存demo/Filters/PopulatePagingDefaultsFilter.cs`

做法：让所有 Query 模型实现 `IPagedQuery`，Filter 在 Action 执行前统一：

- `Page < 1` → 纠正为 1
- `PageSize <= 0` → 填 `DefaultPageSize`
- `PageSize > MaxPageSize` → 封顶

收益：分页规则一致、控制器更干净；且配置改动集中体现在 `appsettings.json`。

### 4) 为什么要 `RowVersion`（乐观锁）

实体：`Product.RowVersion`（见 `进销存demo/Models/Entities/Product.cs`）

库存是高度并发敏感的数据：采购确认、销售出库、退货、盘点、手工调整都可能同时发生。

这里采用“轻量乐观锁”：

- 每次修改商品库存/关键字段时 `RowVersion += 1`
- 通过 EF 的并发异常（`DbUpdateConcurrencyException`）提示用户刷新重试

收益：避免“后写覆盖前写”的静默数据错误，且实现成本低、用户可理解。

### 5) 为什么软删除 + 全局过滤（QueryFilter），以及 10622 警告怎么处理

软删除可以保留历史数据（订单、流水、审计）而不影响日常列表。

实现：

- 实体实现 `ISoftDelete`
- `OnModelCreating` 对主体表设置 `HasQueryFilter(x => !x.IsDeleted)`

注意点：当 **被过滤的主体** 出现在 **必需导航（Required）** 的关系中，EF 会给出 `Model.Validation[10622]` 警告。

本项目的处理策略：

- **保持会计/业务数据不被“连坐过滤”**（例如软删供应商不应导致其历史应付/采购单不可见）
- 将这些关系配置为 **可选导航**（FK 改为可空 + Fluent API `IsRequired(false)`）
- 在 Service 层对“业务必填”的字段做显式校验（例如确认入库时必须有供应商/商品）

## 目录结构（关键）

- `进销存demo/Controllers`：MVC 控制器
- `进销存demo/Controllers/Api`：仪表盘 API
- `进销存demo/Models/Entities`：实体
- `进销存demo/Data`：DbContext、拦截器、初始化
- `进销存demo/Services`：业务服务（采购/销售/库存/批次/Excel/PDF）
- `进销存demo/Views`：Razor 视图
- `进销存demo/wwwroot`：静态资源（含 `lib/echarts/echarts.min.js` 离线包）

