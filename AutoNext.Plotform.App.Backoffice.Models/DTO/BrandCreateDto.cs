
using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class BrandCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(255)]
        public string? WebsiteUrl { get; set; }

        [MaxLength(100)]
        public string? CountryOfOrigin { get; set; }

        public int? FoundedYear { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class BrandUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(100)]
        public string? Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(255)]
        public string? WebsiteUrl { get; set; }

        [MaxLength(100)]
        public string? CountryOfOrigin { get; set; }

        public int? FoundedYear { get; set; }

        public List<string>? ApplicableCategories { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class BrandResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? CountryOfOrigin { get; set; }
        public int? FoundedYear { get; set; }
        public List<string>? ApplicableCategories { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsSelected { get; set; }

    }
}
