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
    public class VehicleModelProfile : Profile
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public VehicleModelProfile()
        {
            // ── CreateDto → Entity ───────────────────────────────
            CreateMap<VehicleModelCreateDto, VehicleModel>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src =>
                        src.Metadata != null
                            ? JsonSerializer.Serialize(src.Metadata, _jsonOptions)
                            : null));

            // ── UpdateDto → Entity ───────────────────────────────
            CreateMap<VehicleModelUpdateDto, VehicleModel>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src =>
                        src.Metadata != null
                            ? JsonSerializer.Serialize(src.Metadata, _jsonOptions)
                            : null));

            // ── Entity → ResponseDto ─────────────────────────────
            CreateMap<VehicleModel, VehicleModelResponseDto>()
                .ForMember(dest => dest.Metadata,
                    opt => opt.MapFrom(src =>
                        !string.IsNullOrWhiteSpace(src.Metadata)
                            ? JsonSerializer.Deserialize<Dictionary<string, object>>(src.Metadata, _jsonOptions)
                            : null));
        }
    }
}
