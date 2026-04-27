using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class DocumentTypeCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool IsVerifiable { get; set; } = false;

        public int? ExpiryMonths { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public int DisplayOrder { get; set; } = 0;
    }

    public class DocumentTypeUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        public bool? IsRequired { get; set; }

        public bool? IsVerifiable { get; set; }

        public int? ExpiryMonths { get; set; }

        public List<string>? ApplicableVehicleTypes { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }
    }

    public class DocumentTypeResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Category { get; set; }
        public bool IsRequired { get; set; }
        public bool IsVerifiable { get; set; }
        public int? ExpiryMonths { get; set; }
        public List<string>? ApplicableVehicleTypes { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class DocumentTypeCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<DocumentTypeResponseDto> DocumentTypes { get; set; } = new List<DocumentTypeResponseDto>();
    }
}
