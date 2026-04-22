using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class ColorCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(7)]
        [RegularExpression(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
            ErrorMessage = "Hex code must be a valid color code (e.g., #FF0000 or #F00)")]
        public string? HexCode { get; set; }

        [MaxLength(50)]
        [RegularExpression(@"^rgb\((\d{1,3},\s*\d{1,3},\s*\d{1,3})\)$",
            ErrorMessage = "RGB value must be in format: rgb(255, 0, 0)")]
        public string? RgbValue { get; set; }

        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 0;
    }

    public class ColorUpdateDto
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(7)]
        [RegularExpression(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
            ErrorMessage = "Hex code must be a valid color code (e.g., #FF0000 or #F00)")]
        public string? HexCode { get; set; }

        [MaxLength(50)]
        [RegularExpression(@"^rgb\((\d{1,3},\s*\d{1,3},\s*\d{1,3})\)$",
            ErrorMessage = "RGB value must be in format: rgb(255, 0, 0)")]
        public string? RgbValue { get; set; }

        public string? Description { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }
    }

    public class ColorResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? HexCode { get; set; }
        public string? RgbValue { get; set; }
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
