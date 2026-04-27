using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class ShippingOptionCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? Provider { get; set; }

        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }

        public decimal? BaseCost { get; set; }
        public decimal? CostPerKm { get; set; }

        public bool IsTrackingAvailable { get; set; }
        public bool IsInsuranceAvailable { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public int DisplayOrder { get; set; } = 0;
    }

    public class ShippingOptionUpdateDto
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? Description { get; set; }
        public string? Provider { get; set; }

        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }

        public decimal? BaseCost { get; set; }
        public decimal? CostPerKm { get; set; }

        public bool? IsTrackingAvailable { get; set; }
        public bool? IsInsuranceAvailable { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ShippingOptionResponseDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? Provider { get; set; }

        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }

        public decimal? BaseCost { get; set; }
        public decimal? CostPerKm { get; set; }

        public bool IsTrackingAvailable { get; set; }
        public bool IsInsuranceAvailable { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
