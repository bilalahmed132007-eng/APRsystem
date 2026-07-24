namespace APRsystem.Authorization
{
    public static class Permissions
    {
        public const string ClaimType = "Permission";

        public const string UsersView = "Users.View";
        public const string UsersCreate = "Users.Create";
        public const string UsersEdit = "Users.Edit";
        public const string UsersDelete = "Users.Delete";

        public const string RolesManage = "Roles.Manage";
        public const string PermissionsManage = "Permissions.Manage";

        public const string DepartmentsManage = "Departments.Manage";
        public const string PositionsManage = "Positions.Manage";
        public const string PostingsManage = "Postings.Manage";
        public const string DashboardView = "Dashboard.View";

        public const string KPIsView = "KPIs.View";
        public const string KPIsManage = "KPIs.Manage";

        public const string ContractsView = "Contracts.View";
        public const string ContractsManage = "Contracts.Manage";

        public const string LookupsManage = "Lookups.Manage";

        public const string AuditLogsView = "AuditLogs.View";

        public static readonly string[] All = new[]
{
    DashboardView,

    UsersView,
    UsersCreate,
    UsersEdit,
    UsersDelete,

    RolesManage,
    PermissionsManage,

    DepartmentsManage,
    PositionsManage,
    PostingsManage,

    KPIsView,
    KPIsManage,

    ContractsView,
    ContractsManage,

    LookupsManage,

    AuditLogsView
};
    }
}