using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class VehicleModelCreateDto
    {
        [Required]
        public Guid BrandId { get; set; }

        [Required]
        public Guid VehicleTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? StartYear { get; set; }
        public int? EndYear { get; set; }

        public bool IsCurrentModel { get; set; } = false;

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class VehicleModelUpdateDto
    {
        public Guid? BrandId { get; set; }
        public Guid? VehicleTypeId { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(100)]
        public string? Slug { get; set; }

        public string? Description { get; set; }

        public int? StartYear { get; set; }
        public int? EndYear { get; set; }

        public bool? IsCurrentModel { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class VehicleModelResponseDto
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public Guid VehicleTypeId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? StartYear { get; set; }
        public int? EndYear { get; set; }

        public bool IsCurrentModel { get; set; }

        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // 🔥 Optional (future ready for joins)
        public string? BrandName { get; set; }
        public string? VehicleTypeName { get; set; }
    }
}
