using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class VehicleVariantCreateDto
    {
        [Required]
        public Guid ModelId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid? FuelTypeId { get; set; }

        public Guid? TransmissionId { get; set; }

        [MaxLength(50)]
        public string? DriveType { get; set; }

        [Range(0, 9999.99)]
        public decimal? EngineSize { get; set; }

        [Range(0, 2000)]
        public int? Horsepower { get; set; }

        [Range(0, 2000)]
        public int? Torque { get; set; }

        [Range(1, 50)]
        public int? SeatingCapacity { get; set; }

        [Range(2, 6)]
        public int? DoorsCount { get; set; }

        [Range(0, 999999999.99)]
        public decimal? BasePrice { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class VehicleVariantUpdateDto
    {
        public Guid? ModelId { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        public string? Description { get; set; }

        public Guid? FuelTypeId { get; set; }

        public Guid? TransmissionId { get; set; }

        [MaxLength(50)]
        public string? DriveType { get; set; }

        [Range(0, 9999.99)]
        public decimal? EngineSize { get; set; }

        [Range(0, 2000)]
        public int? Horsepower { get; set; }

        [Range(0, 2000)]
        public int? Torque { get; set; }

        [Range(1, 50)]
        public int? SeatingCapacity { get; set; }

        [Range(2, 6)]
        public int? DoorsCount { get; set; }

        [Range(0, 999999999.99)]
        public decimal? BasePrice { get; set; }

        public bool? IsAvailable { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class VehicleVariantResponseDto
    {
        public Guid Id { get; set; }
        public Guid ModelId { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string ModelCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? FuelTypeId { get; set; }
        public string? FuelTypeName { get; set; }
        public Guid? TransmissionId { get; set; }
        public string? TransmissionName { get; set; }
        public string? DriveType { get; set; }
        public decimal? EngineSize { get; set; }
        public int? Horsepower { get; set; }
        public int? Torque { get; set; }
        public int? SeatingCapacity { get; set; }
        public int? DoorsCount { get; set; }
        public decimal? BasePrice { get; set; }
        public bool IsAvailable { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class VehicleVariantFilterDto
    {
        public Guid? ModelId { get; set; }
        public Guid? FuelTypeId { get; set; }
        public Guid? TransmissionId { get; set; }
        public string? DriveType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinHorsepower { get; set; }
        public int? MaxHorsepower { get; set; }
        public bool? IsAvailable { get; set; }
        public bool? IsActive { get; set; }
        public int? MinSeatingCapacity { get; set; }
        public int? MaxSeatingCapacity { get; set; }
    }

    public class VehicleVariantSpecsDto
    {
        public decimal? EngineSize { get; set; }
        public int? Horsepower { get; set; }
        public int? Torque { get; set; }
        public int? SeatingCapacity { get; set; }
        public int? DoorsCount { get; set; }
        public string? FuelType { get; set; }
        public string? Transmission { get; set; }
        public string? DriveType { get; set; }
        public decimal? BasePrice { get; set; }
    }
}
