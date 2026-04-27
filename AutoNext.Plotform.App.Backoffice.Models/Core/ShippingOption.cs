using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class ShippingOption
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Shipping option name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Shipping option code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [StringLength(100, ErrorMessage = "Provider cannot exceed 100 characters")]
        public string? Provider { get; set; }

        [Range(0, 365, ErrorMessage = "Estimated days min must be between 0 and 365")]
        public int? EstimatedDaysMin { get; set; }

        [Range(0, 365, ErrorMessage = "Estimated days max must be between 0 and 365")]
        public int? EstimatedDaysMax { get; set; }

        [Range(0, 100000, ErrorMessage = "Base cost must be between 0 and 100,000")]
        public decimal? BaseCost { get; set; }

        [Range(0, 1000, ErrorMessage = "Cost per KM must be between 0 and 1000")]
        public decimal? CostPerKm { get; set; }

        public bool IsTrackingAvailable { get; set; }
        public bool IsInsuranceAvailable { get; set; }

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