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
    public class DocumentTypeProfile : Profile
    {
        // ── JSON Helpers (expression-tree safe) ───────────────────────────────
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static string? SerializeList(List<string>? value)
            => value is not null ? JsonSerializer.Serialize(value, _jsonOptions) : null;

        private static List<string>? DeserializeList(string? json)
            => !string.IsNullOrWhiteSpace(json)
                ? JsonSerializer.Deserialize<List<string>>(json, _jsonOptions)
                : null;

        public DocumentTypeProfile()
        {
            // ── CreateDto ──► Entity ──────────────────────────────────────────
            CreateMap<DocumentTypeCreateDto, DocumentType>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.ApplicableVehicleTypes,
                    opt => opt.MapFrom(src => SerializeList(src.ApplicableVehicleTypes)));

            // ── UpdateDto ──► Entity ──────────────────────────────────────────
            CreateMap<DocumentTypeUpdateDto, DocumentType>()
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
                .ForMember(dest => dest.IsRequired,
                    opt => opt.Condition(src => src.IsRequired.HasValue))
                .ForMember(dest => dest.IsVerifiable,
                    opt => opt.Condition(src => src.IsVerifiable.HasValue))
                .ForMember(dest => dest.ExpiryMonths,
                    opt => opt.Condition(src => src.ExpiryMonths.HasValue))
                .ForMember(dest => dest.DisplayOrder,
                    opt => opt.Condition(src => src.DisplayOrder.HasValue))
                .ForMember(dest => dest.IsActive,
                    opt => opt.Condition(src => src.IsActive.HasValue))
                .ForMember(dest => dest.ApplicableVehicleTypes,
                    opt => opt.MapFrom(src => SerializeList(src.ApplicableVehicleTypes)));

            // ── Entity ──► ResponseDto ────────────────────────────────────────
            CreateMap<DocumentType, DocumentTypeResponseDto>()
                .ForMember(dest => dest.ApplicableVehicleTypes,
                    opt => opt.MapFrom(src => DeserializeList(src.ApplicableVehicleTypes)));
        }
    }
}
