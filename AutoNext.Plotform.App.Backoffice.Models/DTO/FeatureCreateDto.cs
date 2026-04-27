using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class FeatureCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(50)]
        public string? SubCategory { get; set; }

        [MaxLength(500)]
        public string? IconUrl { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public bool IsStandard { get; set; } = false;

        public int DisplayOrder { get; set; } = 0;

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class FeatureUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(50)]
        public string? SubCategory { get; set; }

        [MaxLength(500)]
        public string? IconUrl { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public bool? IsStandard { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class FeatureResponseDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? SubCategory { get; set; }

        public string? IconUrl { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public bool IsStandard { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class FeatureListDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? SubCategory { get; set; }

        public string? IconUrl { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public bool IsStandard { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }
    }
}
