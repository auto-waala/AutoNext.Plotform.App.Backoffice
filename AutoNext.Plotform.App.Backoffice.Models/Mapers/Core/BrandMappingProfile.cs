using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using AutoNext.Plotform.App.Backoffice.Models.ViewModels;

namespace AutoNext.Plotform.App.Backoffice.Models.Mapers.Core;

public class BrandMappingProfile : Profile
{
    public BrandMappingProfile()
    {
        // Domain ↔ Response DTO
        CreateMap<Brand, BrandResponseDto>()
            .ForMember(dest => dest.IsSelected, opt => opt.Ignore())
            .ReverseMap();

        // ViewModel → DTOs (used in UI save)
        CreateMap<BrandFormModel, BrandCreateDto>()
            .ForMember(dest => dest.ApplicableCategories, opt => opt.MapFrom(src => src.ApplicableCategories))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata));

        CreateMap<BrandFormModel, BrandUpdateDto>()
            .ForMember(dest => dest.ApplicableCategories, opt => opt.MapFrom(src => src.ApplicableCategories))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        // Response DTO → ViewModel (edit scenario)
        CreateMap<BrandResponseDto, BrandFormModel>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata ?? new Dictionary<string, object>()));

        // Response DTO → Brand (for sidebar)
        CreateMap<BrandResponseDto, Brand>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Code, opt => opt.MapFrom(src => src.Code))
            .ForMember(dest => dest.Slug, opt => opt.MapFrom(src => src.Slug))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.LogoUrl, opt => opt.MapFrom(src => src.LogoUrl))
            .ForMember(dest => dest.WebsiteUrl, opt => opt.MapFrom(src => src.WebsiteUrl))
            .ForMember(dest => dest.CountryOfOrigin, opt => opt.MapFrom(src => src.CountryOfOrigin))
            .ForMember(dest => dest.FoundedYear, opt => opt.MapFrom(src => src.FoundedYear))
            .ForMember(dest => dest.ApplicableCategories, opt => opt.MapFrom(src => src.ApplicableCategories ?? new List<string>()))
            .ForMember(dest => dest.DisplayOrder, opt => opt.MapFrom(src => src.DisplayOrder))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata ?? new Dictionary<string, object>()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt));

        // Brand → Response DTO
        CreateMap<Brand, BrandResponseDto>()
            .ForMember(dest => dest.IsSelected, opt => opt.Ignore());
    }
}