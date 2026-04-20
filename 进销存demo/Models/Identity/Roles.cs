namespace 进销存demo.Models.Identity
{
    /// <summary>
    /// 4 个内置角色的常量（便于 [Authorize(Roles = ...)] 引用避免拼写错误）。
    /// </summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Purchaser = "Purchaser";
        public const string Salesperson = "Salesperson";
        public const string Warehouse = "Warehouse";

        public const string AdminOrPurchaser = Admin + "," + Purchaser;
        public const string AdminOrSalesperson = Admin + "," + Salesperson;
        public const string AdminOrWarehouse = Admin + "," + Warehouse;

        public static readonly string[] All = { Admin, Purchaser, Salesperson, Warehouse };

        public static string Display(string role) => role switch
        {
            Admin => "管理员",
            Purchaser => "采购员",
            Salesperson => "销售员",
            Warehouse => "库管",
            _ => role
        };
    }
}
