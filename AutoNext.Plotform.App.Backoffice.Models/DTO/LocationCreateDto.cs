using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class LocationCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string CountryName { get; set; } = string.Empty;

        [Required]
        [MaxLength(5)]
        public string CountryCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string StateName { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string StateCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CityName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? District { get; set; }

        [MaxLength(10)]
        public string? Pincode { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class LocationResponseDto
    {
        public Guid Id { get; set; }
        public string CountryName { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string StateName { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? Pincode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public List<CityAreaDto>? Areas { get; set; }
    }
}
