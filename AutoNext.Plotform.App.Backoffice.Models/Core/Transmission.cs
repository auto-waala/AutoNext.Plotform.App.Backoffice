using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class Transmission
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Transmission name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Transmission code is required")]
        [StringLength(20, ErrorMessage = "Code cannot exceed 20 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Range(1, 20, ErrorMessage = "Gears count must be between 1 and 20")]
        public int? GearsCount { get; set; }

        [Range(0, 9999, ErrorMessage = "Sort order must be between 0 and 9999")]
        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}