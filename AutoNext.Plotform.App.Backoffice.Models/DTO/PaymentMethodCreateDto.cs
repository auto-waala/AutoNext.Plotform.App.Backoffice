using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class PaymentMethodCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(500)]
        public string? IconUrl { get; set; }

        public decimal ProcessingFeePercentage { get; set; } = 0;
        public decimal ProcessingFeeFixed { get; set; } = 0;

        public int? SettlementDays { get; set; }

        public bool IsInstant { get; set; } = false;
        public bool IsAvailableForSellers { get; set; } = true;
        public bool IsAvailableForBuyers { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class PaymentMethodUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(500)]
        public string? IconUrl { get; set; }

        public decimal? ProcessingFeePercentage { get; set; }
        public decimal? ProcessingFeeFixed { get; set; }

        public int? SettlementDays { get; set; }

        public bool? IsInstant { get; set; }
        public bool? IsAvailableForSellers { get; set; }
        public bool? IsAvailableForBuyers { get; set; }

        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class PaymentMethodResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? IconUrl { get; set; }
        public decimal ProcessingFeePercentage { get; set; }
        public decimal ProcessingFeeFixed { get; set; }
        public int? SettlementDays { get; set; }
        public bool IsInstant { get; set; }
        public bool IsAvailableForSellers { get; set; }
        public bool IsAvailableForBuyers { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
