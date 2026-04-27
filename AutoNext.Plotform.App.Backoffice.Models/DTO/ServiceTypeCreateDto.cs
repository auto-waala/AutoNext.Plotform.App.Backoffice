using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class ServiceTypeCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        public int? IntervalMonths { get; set; }
        public int? IntervalKm { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? IconUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;
    }

    public class ServiceTypeUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        public int? IntervalMonths { get; set; }
        public int? IntervalKm { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? IconUrl { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }
    }

    public class ServiceTypeResponseDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public string? Category { get; set; }

        public int? IntervalMonths { get; set; }
        public int? IntervalKm { get; set; }

        public string? Description { get; set; }
        public string? IconUrl { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
