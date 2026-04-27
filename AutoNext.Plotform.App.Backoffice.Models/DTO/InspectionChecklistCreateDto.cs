using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class InspectionChecklistCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public bool IsCritical { get; set; } = false;

        [Range(1, 10)]
        public int Weightage { get; set; } = 1;

        public int DisplayOrder { get; set; } = 0;
    }

    public class InspectionChecklistUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public bool? IsCritical { get; set; }

        [Range(1, 10)]
        public int? Weightage { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }
    }

    public class InspectionChecklistResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Category { get; set; }
        public List<string>? ApplicableVehicleTypes { get; set; }
        public bool IsCritical { get; set; }
        public int Weightage { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
