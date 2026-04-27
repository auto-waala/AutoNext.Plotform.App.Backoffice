using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class DocumentType
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Document type name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Document type code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
        public string? Category { get; set; }

        public bool IsRequired { get; set; }
        public bool IsVerifiable { get; set; }

        [Range(0, 1200, ErrorMessage = "Expiry months must be between 0 and 1200")]
        public int? ExpiryMonths { get; set; }

        [StringLength(500, ErrorMessage = "Applicable vehicle types cannot exceed 500 characters")]
        public string? ApplicableVehicleTypes { get; set; }

        [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}