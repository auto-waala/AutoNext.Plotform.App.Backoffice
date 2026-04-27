using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.Mapers.Core
{
    public class TaxRateProfile : Profile
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TaxRateProfile()
        {
            // Create
            CreateMap<TaxRateCreateDto, TaxRate>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.IsActive, o => o.MapFrom(_ => true))
                .ForMember(d => d.AppliesToVehicleTypes,
                    o => o.MapFrom(s => s.AppliesToVehicleTypes != null
                        ? JsonSerializer.Serialize(s.AppliesToVehicleTypes, _jsonOptions)
                        : null));

            // Update
            CreateMap<TaxRateUpdateDto, TaxRate>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.AppliesToVehicleTypes,
                    o => o.MapFrom(s => s.AppliesToVehicleTypes != null
                        ? JsonSerializer.Serialize(s.AppliesToVehicleTypes, _jsonOptions)
                        : null));

            // Response
            CreateMap<TaxRate, TaxRateResponseDto>()
                .ForMember(d => d.AppliesToVehicleTypes,
                    o => o.MapFrom(s =>
                        !string.IsNullOrWhiteSpace(s.AppliesToVehicleTypes)
                            ? JsonSerializer.Deserialize<List<string>>(s.AppliesToVehicleTypes, _jsonOptions)
                            : null));
        }
    }
}
