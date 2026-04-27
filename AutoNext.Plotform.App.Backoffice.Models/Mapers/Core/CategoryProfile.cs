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
    public class CategoryProfile : Profile
    {
        // ── JSON Helpers (no optional args = expression-tree safe) ────────────
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static string? SerializeObject<T>(T? value) where T : class
            => value is not null ? JsonSerializer.Serialize(value, _jsonOptions) : null;

        private static Dictionary<string, object>? DeserializeDict(string? json)
            => !string.IsNullOrWhiteSpace(json)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions)
                : null;

        private static List<string>? DeserializeList(string? json)
            => !string.IsNullOrWhiteSpace(json)
                ? JsonSerializer.Deserialize<List<string>>(json, _jsonOptions)
                : null;

        public CategoryProfile()
        {
            // ── CreateDto ──► Entity ──────────────────────────────────────────
            CreateMap<CategoryCreateDto, Category>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.SubCategories,
                    opt => opt.Ignore());

            // ── UpdateDto ──► Entity ──────────────────────────────────────────
            CreateMap<CategoryUpdateDto, Category>()
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
                .ForMember(dest => dest.Slug,
                    opt => opt.Condition(src => src.Slug != null))
                .ForMember(dest => dest.Description,
                    opt => opt.Condition(src => src.Description != null))
                .ForMember(dest => dest.IconUrl,
                    opt => opt.Condition(src => src.IconUrl != null))
                .ForMember(dest => dest.ImageUrl,
                    opt => opt.Condition(src => src.ImageUrl != null))
                .ForMember(dest => dest.ParentCategoryId,
                    opt => opt.Condition(src => src.ParentCategoryId.HasValue))
                .ForMember(dest => dest.DisplayOrder,
                    opt => opt.Condition(src => src.DisplayOrder.HasValue))
                .ForMember(dest => dest.IsActive,
                    opt => opt.Condition(src => src.IsActive.HasValue))
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src => SerializeObject(src.Metadata)))
                .ForMember(dest => dest.SubCategories,
                    opt => opt.Ignore());

            // ── Entity ──► ResponseDto ────────────────────────────────────────
            CreateMap<Category, CategoryResponseDto>()
                .ForMember(dest => dest.SubCategories,
                    opt => opt.MapFrom(src => src.SubCategories))
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.MapFrom(src => src.UpdatedAt));

            // ── Entity ──► TreeDto ────────────────────────────────────────────
            CreateMap<Category, CategoryTreeDto>()
                .ForMember(dest => dest.Children,
                    opt => opt.MapFrom(src => src.SubCategories))
                .ForMember(dest => dest.DisplayOrder,
                    opt => opt.MapFrom(src => src.DisplayOrder))
                .ForMember(dest => dest.IconUrl,
                    opt => opt.MapFrom(src => src.IconUrl));

        }
    }
}
