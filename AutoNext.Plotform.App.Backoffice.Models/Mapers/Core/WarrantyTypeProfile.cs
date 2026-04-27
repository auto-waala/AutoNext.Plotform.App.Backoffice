using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Models.Mapers.Core
{
    public class WarrantyTypeProfile : Profile
    {
        public WarrantyTypeProfile()
        {
            // ── CreateDto ──► Entity ──────────────────────────────────────────
            CreateMap<WarrantyTypeCreateDto, WarrantyType>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(_ => true));

            // ── UpdateDto ──► Entity ──────────────────────────────────────────
            CreateMap<WarrantyTypeUpdateDto, WarrantyType>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Name,
                    opt => opt.Condition(src => src.Name != null))
                .ForMember(dest => dest.Code,
                    opt => opt.Condition(src => src.Code != null))
                .ForMember(dest => dest.Description,
                    opt => opt.Condition(src => src.Description != null))
                .ForMember(dest => dest.DurationMonths,
                    opt => opt.Condition(src => src.DurationMonths.HasValue))
                .ForMember(dest => dest.DurationKm,
                    opt => opt.Condition(src => src.DurationKm.HasValue))
                .ForMember(dest => dest.IsTransferable,
                    opt => opt.Condition(src => src.IsTransferable.HasValue))
                .ForMember(dest => dest.DisplayOrder,
                    opt => opt.Condition(src => src.DisplayOrder.HasValue))
                .ForMember(dest => dest.IsActive,
                    opt => opt.Condition(src => src.IsActive.HasValue));


            // ── FilterDto ──► Entity ──────────────────────────────────────────
            CreateMap<WarrantyTypeFilterDto, WarrantyType>()
                .ForAllMembers(opt => opt.Condition(
                    (src, dest, srcMember) => srcMember != null));
        }
    }
}
