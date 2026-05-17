
namespace AutoNext.Plotform.App.Backoffice.Models.Blob
{
    public class FileMetadata
    {
        public string Id { get; set; } = string.Empty;

        public Guid FileId { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string OriginalName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string FileUrl { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string ContentType { get; set; } = string.Empty;

        public string FileHash { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastAccessed { get; set; }

        public int DownloadCount { get; set; } = 0;

        public Dictionary<string, string> CustomMetadata { get; set; } = new();

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        public string ETag { get; set; } = string.Empty;
    }
}
