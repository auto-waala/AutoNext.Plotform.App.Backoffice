using Microsoft.AspNetCore.Authorization;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public static class AuthorizationPolicies
    {
        public static void AddPolicies(AuthorizationOptions options)
        {
            // Role-based policies
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("Admin", "SuperAdmin"));

            options.AddPolicy("SuperAdminOnly", policy =>
                policy.RequireRole("SuperAdmin"));

            options.AddPolicy("ManagerOnly", policy =>
                policy.RequireRole("Admin", "Manager"));

            options.AddPolicy("AuthenticatedUser", policy =>
                policy.RequireAuthenticatedUser());

            // Brand Management Policies
            options.AddPolicy("Brands.View", policy =>
                policy.Requirements.Add(new PermissionRequirement("Brands.View")));

            options.AddPolicy("Brands.Create", policy =>
                policy.Requirements.Add(new PermissionRequirement("Brands.Create")));

            options.AddPolicy("Brands.Edit", policy =>
                policy.Requirements.Add(new PermissionRequirement("Brands.Edit")));

            options.AddPolicy("Brands.Delete", policy =>
                policy.Requirements.Add(new PermissionRequirement("Brands.Delete")));

            // User Management Policies
            options.AddPolicy("Users.View", policy =>
                policy.Requirements.Add(new PermissionRequirement("Users.View")));

            options.AddPolicy("Users.Create", policy =>
                policy.Requirements.Add(new PermissionRequirement("Users.Create")));

            options.AddPolicy("Users.Edit", policy =>
                policy.Requirements.Add(new PermissionRequirement("Users.Edit")));

            options.AddPolicy("Users.Delete", policy =>
                policy.Requirements.Add(new PermissionRequirement("Users.Delete")));

            // Settings Policies
            options.AddPolicy("Settings.View", policy =>
                policy.Requirements.Add(new PermissionRequirement("Settings.View")));

            options.AddPolicy("Settings.Edit", policy =>
                policy.Requirements.Add(new PermissionRequirement("Settings.Edit")));
        }
    }
}
