using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class TaxRate
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Tax rate name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tax rate code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Tax type cannot exceed 50 characters")]
        public string? TaxType { get; set; }

        [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
        public string? Country { get; set; }

        [StringLength(100, ErrorMessage = "State cannot exceed 100 characters")]
        public string? State { get; set; }

        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
        public string? City { get; set; }

        [Range(0, 100, ErrorMessage = "Rate percentage must be between 0 and 100")]
        public decimal RatePercentage { get; set; }

        public bool IsCompound { get; set; }

        [StringLength(500, ErrorMessage = "Applies to vehicle types cannot exceed 500 characters")]
        public string? AppliesToVehicleTypes { get; set; }

        [Range(0, 10000000, ErrorMessage = "Min price threshold must be between 0 and 10,000,000")]
        public decimal? MinPriceThreshold { get; set; }

        [Range(0, 10000000, ErrorMessage = "Max price threshold must be between 0 and 10,000,000")]
        public decimal? MaxPriceThreshold { get; set; }

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}