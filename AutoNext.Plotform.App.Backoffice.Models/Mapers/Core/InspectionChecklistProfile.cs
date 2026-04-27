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
    public class InspectionChecklistProfile : Profile
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public InspectionChecklistProfile()
        {
            // ── CreateDto → Entity ─────────────────────────────────────────
            CreateMap<InspectionChecklistCreateDto, InspectionChecklist>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.ApplicableVehicleTypes,
                    opt => opt.MapFrom(src =>
                        src.ApplicableVehicleTypes != null
                            ? JsonSerializer.Serialize(src.ApplicableVehicleTypes, _jsonOptions)
                            : null));

            // ── UpdateDto → Entity ─────────────────────────────────────────
            CreateMap<InspectionChecklistUpdateDto, InspectionChecklist>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.ApplicableVehicleTypes,
                    opt => opt.MapFrom(src =>
                        src.ApplicableVehicleTypes != null
                            ? JsonSerializer.Serialize(src.ApplicableVehicleTypes, _jsonOptions)
                            : null));

            // ── Entity → ResponseDto ───────────────────────────────────────
            CreateMap<InspectionChecklist, InspectionChecklistResponseDto>()
                .ForMember(dest => dest.ApplicableVehicleTypes,
                    opt => opt.MapFrom(src =>
                        !string.IsNullOrWhiteSpace(src.ApplicableVehicleTypes)
                            ? JsonSerializer.Deserialize<List<string>>(src.ApplicableVehicleTypes, _jsonOptions)
                            : null));
        }
    }
}
