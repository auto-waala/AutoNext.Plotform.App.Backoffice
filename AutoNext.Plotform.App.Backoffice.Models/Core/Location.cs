using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class Location
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Country name is required")]
        [StringLength(100, ErrorMessage = "Country name cannot exceed 100 characters")]
        public string CountryName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Country code is required")]
        [StringLength(5, ErrorMessage = "Country code cannot exceed 5 characters")]
        [RegularExpression(@"^[A-Z]{2,3}$", ErrorMessage = "Country code must be 2-3 uppercase letters (ISO)")]
        public string CountryCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "State name is required")]
        [StringLength(100, ErrorMessage = "State name cannot exceed 100 characters")]
        public string StateName { get; set; } = string.Empty;

        [Required(ErrorMessage = "State code is required")]
        [StringLength(10, ErrorMessage = "State code cannot exceed 10 characters")]
        public string StateCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "City name is required")]
        [StringLength(100, ErrorMessage = "City name cannot exceed 100 characters")]
        public string CityName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "District cannot exceed 100 characters")]
        public string? District { get; set; }

        [StringLength(10, ErrorMessage = "Pincode cannot exceed 10 characters")]
        [RegularExpression(@"^\d{4,10}$", ErrorMessage = "Pincode must contain only numbers")]
        public string? Pincode { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsSelected { get; set; } = false;
    }
}