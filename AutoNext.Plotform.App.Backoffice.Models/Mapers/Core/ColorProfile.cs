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
    public class ColorProfile : Profile
    {
        public ColorProfile()
        {
            // Create DTO to Entity mapping
            CreateMap<ColorCreateDto, Color>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true));

            // Update DTO to Entity mapping
            CreateMap<ColorUpdateDto, Color>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Name, opt => opt.Condition(src => src.Name != null))
                .ForMember(dest => dest.Code, opt => opt.Condition(src => src.Code != null))
                .ForMember(dest => dest.HexCode, opt => opt.Condition(src => src.HexCode != null))
                .ForMember(dest => dest.RgbValue, opt => opt.Condition(src => src.RgbValue != null))
                .ForMember(dest => dest.Description, opt => opt.Condition(src => src.Description != null))
                .ForMember(dest => dest.DisplayOrder, opt => opt.Condition(src => src.DisplayOrder.HasValue))
                .ForMember(dest => dest.IsActive, opt => opt.Condition(src => src.IsActive.HasValue));

            // Entity to Response DTO mapping
            CreateMap<Color, ColorResponseDto>();
        }
    }
}
