using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class Feature
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Feature name is required")]
        [StringLength(100, ErrorMessage = "Feature name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Feature code is required")]
        [StringLength(50, ErrorMessage = "Feature code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
        public string? Category { get; set; }

        [StringLength(50, ErrorMessage = "Sub-category cannot exceed 50 characters")]
        public string? SubCategory { get; set; }

        [Url(ErrorMessage = "Invalid URL format for icon")]
        [StringLength(500, ErrorMessage = "Icon URL cannot exceed 500 characters")]
        public string? IconUrl { get; set; }

        [StringLength(1000, ErrorMessage = "Applicable categories cannot exceed 1000 characters")]
        public string? ApplicableCategories { get; set; }

        public bool IsStandard { get; set; }

        [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        [StringLength(2000, ErrorMessage = "Metadata cannot exceed 2000 characters")]
        public string? Metadata { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}