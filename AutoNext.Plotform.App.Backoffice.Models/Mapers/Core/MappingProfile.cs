using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.Mapers.Core
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<LocationCreateDto, Location>()
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true));

            CreateMap<Location, LocationResponseDto>();
            CreateMap<CityArea, CityAreaDto>();

            // Vehicle Type mappings
            CreateMap<VehicleType, VehicleTypeResponseDto>();
            CreateMap<VehicleTypeCreateDto, VehicleType>();
            CreateMap<VehicleTypeUpdateDto, VehicleType>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Fuel Type mappings
            CreateMap<FuelType, FuelTypeResponseDto>();
            CreateMap<FuelTypeCreateDto, FuelType>();
            CreateMap<FuelTypeUpdateDto, FuelType>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Transmission mappings
            CreateMap<Transmission, TransmissionResponseDto>();
            CreateMap<TransmissionCreateDto, Transmission>();
            CreateMap<TransmissionUpdateDto, Transmission>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        }
    }
}
