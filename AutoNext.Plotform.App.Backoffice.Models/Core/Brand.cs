using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class Brand
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Brand name is required")]
        [StringLength(100, ErrorMessage = "Brand name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Brand code is required")]
        [StringLength(50, ErrorMessage = "Brand code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Slug cannot exceed 200 characters")]
        [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$",
            ErrorMessage = "Slug must be URL-friendly (lowercase letters, numbers, and hyphens only)")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Url(ErrorMessage = "Invalid URL format for Logo")]
        [StringLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters")]
        public string? LogoUrl { get; set; }

        [Url(ErrorMessage = "Invalid URL format for Website")]
        [StringLength(500, ErrorMessage = "Website URL cannot exceed 500 characters")]
        public string? WebsiteUrl { get; set; }

        [StringLength(100, ErrorMessage = "Country name cannot exceed 100 characters")]
        public string? CountryOfOrigin { get; set; }

        [Range(1700, 2025, ErrorMessage = "Year must be between 1700 and 2025")]
        public int? FoundedYear { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}