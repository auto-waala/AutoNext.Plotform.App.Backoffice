using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class CategoryUpdateDto
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
        public string? IconUrl { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public Guid? ParentCategoryId { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

}
