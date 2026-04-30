namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class UserSessionDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string UserType { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiry { get; set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && TokenExpiry > DateTime.UtcNow;
        public bool IsTokenExpiringSoon => TokenExpiry > DateTime.UtcNow && TokenExpiry - DateTime.UtcNow < TimeSpan.FromMinutes(5);
    }
}
