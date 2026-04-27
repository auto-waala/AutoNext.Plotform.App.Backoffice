using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class WarrantyType
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Warranty type name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Warranty type code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Range(0, 1200, ErrorMessage = "Duration months must be between 0 and 1200")]
        public int? DurationMonths { get; set; }

        [Range(0, 1000000, ErrorMessage = "Duration KM must be between 0 and 1,000,000")]
        public int? DurationKm { get; set; }

        public bool IsTransferable { get; set; }

        [StringLength(500, ErrorMessage = "Applicable categories cannot exceed 500 characters")]
        public string? ApplicableCategories { get; set; }

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