using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using AutoNext.Plotform.App.Backoffice.Models.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.Mapers.Core
{
    public class FeatureProfile : Profile
    {
        public FeatureProfile()
        {
            // ── CreateDto ──► Entity ──────────────────────────────────────────
            CreateMap<FeatureCreateDto, Feature>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.ApplicableCategories,
                    opt => opt.MapFrom(src => JsonMappingHelpers.SerializeList(src.ApplicableCategories)))
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src => JsonMappingHelpers.SerializeObject(src.Metadata)));

            // ── UpdateDto ──► Entity ──────────────────────────────────────────
            CreateMap<FeatureUpdateDto, Feature>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.IsActive,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Name,
                    opt => opt.Condition(src => src.Name != null))
                .ForMember(dest => dest.Code,
                    opt => opt.Condition(src => src.Code != null))
                .ForMember(dest => dest.Category,
                    opt => opt.Condition(src => src.Category != null))
                .ForMember(dest => dest.SubCategory,
                    opt => opt.Condition(src => src.SubCategory != null))
                .ForMember(dest => dest.IconUrl,
                    opt => opt.Condition(src => src.IconUrl != null))
                .ForMember(dest => dest.IsStandard,
                    opt => opt.Condition(src => src.IsStandard.HasValue))
                .ForMember(dest => dest.DisplayOrder,
                    opt => opt.Condition(src => src.DisplayOrder.HasValue))
                .ForMember(dest => dest.ApplicableCategories,
                    opt => opt.MapFrom(src => JsonMappingHelpers.SerializeList(src.ApplicableCategories)))
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src => JsonMappingHelpers.SerializeObject(src.Metadata)));

            // ── Entity ──► ResponseDto ────────────────────────────────────────
            CreateMap<Feature, FeatureResponseDto>()
                .ForMember(dest => dest.ApplicableCategories,
                    opt => opt.MapFrom(src => JsonMappingHelpers.DeserializeList(src.ApplicableCategories)))
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src => JsonMappingHelpers.DeserializeDict(src.Metadata)));

            // ── Entity ──► ListDto ────────────────────────────────────────────
            CreateMap<Feature, FeatureListDto>()
                .ForMember(dest => dest.ApplicableCategories,
                    opt => opt.MapFrom(src => JsonMappingHelpers.DeserializeList(src.ApplicableCategories)));
        }
    }
}
