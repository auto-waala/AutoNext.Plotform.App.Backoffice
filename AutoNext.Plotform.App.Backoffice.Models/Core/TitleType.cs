using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class TitleType
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Title type name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title type code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public bool IsClean { get; set; }

        public bool AffectsValue { get; set; }

        [Range(0, 100, ErrorMessage = "Value deduction percentage must be between 0 and 100")]
        public decimal? ValueDeductionPercentage { get; set; }

        [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}