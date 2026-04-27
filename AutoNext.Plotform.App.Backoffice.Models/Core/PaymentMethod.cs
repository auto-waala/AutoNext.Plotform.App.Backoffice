using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class PaymentMethod
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Payment method name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Payment method code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
        public string? Type { get; set; }

        [Url(ErrorMessage = "Invalid URL format for icon")]
        [StringLength(500, ErrorMessage = "Icon URL cannot exceed 500 characters")]
        public string? IconUrl { get; set; }

        [Range(0, 100, ErrorMessage = "Processing fee percentage must be between 0 and 100")]
        public decimal ProcessingFeePercentage { get; set; }

        [Range(0, 10000, ErrorMessage = "Fixed fee must be between 0 and 10000")]
        public decimal ProcessingFeeFixed { get; set; }

        [Range(0, 365, ErrorMessage = "Settlement days must be between 0 and 365")]
        public int? SettlementDays { get; set; }

        public bool IsInstant { get; set; }
        public bool IsAvailableForSellers { get; set; }
        public bool IsAvailableForBuyers { get; set; }

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