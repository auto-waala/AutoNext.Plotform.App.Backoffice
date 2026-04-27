using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class WarrantyTypeCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(1, 1200)]
        public int? DurationMonths { get; set; }

        [Range(1, 1000000)]
        public int? DurationKm { get; set; }

        public bool IsTransferable { get; set; } = false;

        public List<string>? ApplicableCategories { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class WarrantyTypeUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        public string? Description { get; set; }

        [Range(1, 1200)]
        public int? DurationMonths { get; set; }

        [Range(1, 1000000)]
        public int? DurationKm { get; set; }

        public bool? IsTransferable { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class WarrantyTypeResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? DurationMonths { get; set; }
        public int? DurationKm { get; set; }
        public string? FormattedDuration { get; set; }
        public bool IsTransferable { get; set; }
        public List<string>? ApplicableCategories { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class WarrantyTypeFilterDto
    {
        public bool? IsActive { get; set; }
        public bool? IsTransferable { get; set; }
        public string? CategoryCode { get; set; }
        public int? MinDurationMonths { get; set; }
        public int? MaxDurationMonths { get; set; }
        public int? MinDurationKm { get; set; }
        public int? MaxDurationKm { get; set; }
    }

    public class WarrantyComparisonDto
    {
        public WarrantyTypeResponseDto WarrantyA { get; set; } = new WarrantyTypeResponseDto();
        public WarrantyTypeResponseDto WarrantyB { get; set; } = new WarrantyTypeResponseDto();
        public WarrantyComparisonResultDto Comparison { get; set; } = new WarrantyComparisonResultDto();
    }

    public class WarrantyComparisonResultDto
    {
        public bool SameDuration { get; set; }
        public bool SameTransferability { get; set; }
        public string? BetterCoverage { get; set; }
        public int DurationDifferenceMonths { get; set; }
        public int DurationDifferenceKm { get; set; }
    }
}
