using Microsoft.AspNetCore.Authorization;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ILogger<PermissionAuthorizationHandler> _logger;

        public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var hasPermission = context.User.HasClaim("Permission", requirement.Permission);
            var isAdmin = context.User.IsInRole("Admin") || context.User.IsInRole("SuperAdmin");

            if (hasPermission || isAdmin)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {User} denied access for permission: {Permission}",
                    context.User.Identity?.Name, requirement.Permission);
            }

            return Task.CompletedTask;
        }
    }

    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
        }
    }
}
