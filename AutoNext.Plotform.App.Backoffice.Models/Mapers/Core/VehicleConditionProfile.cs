using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.Mapers.Core
{
    public class VehicleConditionProfile : Profile
    {
        public VehicleConditionProfile()
        {
            // Create
            CreateMap<VehicleConditionCreateDto, VehicleCondition>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore())
                .ForMember(d => d.IsActive, o => o.MapFrom(_ => true));

            // Update
            CreateMap<VehicleConditionUpdateDto, VehicleCondition>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore());

            // Response
            CreateMap<VehicleCondition, VehicleConditionResponseDto>();
        }
    }
}
