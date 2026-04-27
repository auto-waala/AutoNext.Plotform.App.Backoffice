using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class CityArea
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public Guid? LocationId { get; set; }

        [Required(ErrorMessage = "Area name is required")]
        [StringLength(200, ErrorMessage = "Area name cannot exceed 200 characters")]
        public string AreaName { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "Area code cannot exceed 20 characters")]
        public string? AreaCode { get; set; }

        [StringLength(10, ErrorMessage = "Pincode cannot exceed 10 characters")]
        [RegularExpression(@"^\d{4,10}$", ErrorMessage = "Pincode must contain only numbers (4-10 digits)")]
        public string? Pincode { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}