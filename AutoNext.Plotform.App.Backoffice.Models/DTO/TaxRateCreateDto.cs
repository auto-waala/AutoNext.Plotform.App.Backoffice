using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class TaxRateCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        public string? TaxType { get; set; }

        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }

        [Required]
        public decimal RatePercentage { get; set; }

        public bool IsCompound { get; set; }

        public List<string>? AppliesToVehicleTypes { get; set; }

        public decimal? MinPriceThreshold { get; set; }
        public decimal? MaxPriceThreshold { get; set; }

        [Required]
        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public int DisplayOrder { get; set; } = 0;
    }

    public class TaxRateUpdateDto
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? TaxType { get; set; }

        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }

        public decimal? RatePercentage { get; set; }
        public bool? IsCompound { get; set; }

        public List<string>? AppliesToVehicleTypes { get; set; }

        public decimal? MinPriceThreshold { get; set; }
        public decimal? MaxPriceThreshold { get; set; }

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TaxRateResponseDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public string? TaxType { get; set; }

        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }

        public decimal RatePercentage { get; set; }
        public bool IsCompound { get; set; }

        public List<string>? AppliesToVehicleTypes { get; set; }

        public decimal? MinPriceThreshold { get; set; }
        public decimal? MaxPriceThreshold { get; set; }

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
