using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class VehicleModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Brand is required")]
        public Guid? BrandId { get; set; }

        [Required(ErrorMessage = "Model name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Model code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required")]
        [StringLength(100, ErrorMessage = "Slug cannot exceed 100 characters")]
        [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug must be URL-friendly")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vehicle type is required")]
        public Guid? VehicleTypeId { get; set; }

        [Range(1900, 2030, ErrorMessage = "Start year must be between 1900 and 2030")]
        public int? StartYear { get; set; }

        [Range(1900, 2030, ErrorMessage = "End year must be between 1900 and 2030")]
        public int? EndYear { get; set; }

        public bool IsCurrentModel { get; set; }

        [Url(ErrorMessage = "Invalid URL format for image")]
        [StringLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
        public string? ImageUrl { get; set; }

        [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        [StringLength(2000, ErrorMessage = "Metadata cannot exceed 2000 characters")]
        public string? Metadata { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}