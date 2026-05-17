
namespace AutoNext.Plotform.App.Backoffice.Models.Blob
{
    public class Client
    {
        public string Id { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public long StorageQuota { get; set; } = 1073741824;

        public long UsedStorage { get; set; } = 0;

        public List<string> AllowedFileTypes { get; set; } = new();

        public long MaxFileSize { get; set; } = 52428800;

        public int RateLimitPerMinute { get; set; } = 100;

        public List<string> IpWhitelist { get; set; } = new();

        public DateTime? LastAccessed { get; set; }

        public string Environment { get; set; } = "Production";
    }
}
