using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.Core
{
    public class VehicleVariant
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Model is required")]
        public Guid? ModelId { get; set; }

        [Required(ErrorMessage = "Variant name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Variant code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Code can only contain uppercase letters, numbers, and underscores")]
        public string Code { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public Guid? FuelTypeId { get; set; }
        public Guid? TransmissionId { get; set; }

        [StringLength(50, ErrorMessage = "Drive type cannot exceed 50 characters")]
        public string? DriveType { get; set; }

        [Range(0.5, 20, ErrorMessage = "Engine size must be between 0.5L and 20L")]
        public decimal? EngineSize { get; set; }

        [Range(0, 2000, ErrorMessage = "Horsepower must be between 0 and 2000")]
        public int? Horsepower { get; set; }

        [Range(0, 2000, ErrorMessage = "Torque must be between 0 and 2000")]
        public int? Torque { get; set; }

        [Range(1, 20, ErrorMessage = "Seating capacity must be between 1 and 20")]
        public int? SeatingCapacity { get; set; }

        [Range(2, 6, ErrorMessage = "Doors count must be between 2 and 6")]
        public int? DoorsCount { get; set; }

        [Range(0, 10000000, ErrorMessage = "Base price must be between 0 and 10,000,000")]
        public decimal? BasePrice { get; set; }

        public bool IsAvailable { get; set; }

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