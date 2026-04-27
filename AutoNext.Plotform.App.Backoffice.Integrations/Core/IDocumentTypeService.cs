using AutoNext.Plotform.App.Backoffice.Models.DTO;


namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IDocumentTypeService
    {
        Task<DocumentTypeResponseDto?> GetDocumentTypeByIdAsync(Guid documentTypeId);
        Task<DocumentTypeResponseDto?> GetDocumentTypeByCodeAsync(string code);
        Task<IEnumerable<DocumentTypeResponseDto>> GetAllDocumentTypesAsync();
        Task<IEnumerable<DocumentTypeResponseDto>> GetActiveDocumentTypesAsync();
        Task<IEnumerable<DocumentTypeResponseDto>> GetDocumentTypesByCategoryAsync(string category);
        Task<IEnumerable<DocumentTypeResponseDto>> GetRequiredDocumentTypesAsync();
        Task<IEnumerable<DocumentTypeResponseDto>> GetVerifiableDocumentTypesAsync();
        Task<IEnumerable<DocumentTypeResponseDto>> GetDocumentTypesByVehicleTypeAsync(string vehicleType);
        Task<IEnumerable<DocumentTypeCategoryDto>> GetDocumentTypesGroupedByCategoryAsync();
        Task<DocumentTypeResponseDto> CreateDocumentTypeAsync(DocumentTypeCreateDto createDto);
        Task<DocumentTypeResponseDto?> UpdateDocumentTypeAsync(Guid documentTypeId, DocumentTypeUpdateDto updateDto);
        Task<bool> DeleteDocumentTypeAsync(Guid documentTypeId);
        Task<bool> ToggleDocumentTypeStatusAsync(Guid documentTypeId, bool isActive);
        Task<bool> ReorderDocumentTypesAsync(Dictionary<Guid, int> orderMap);
        Task<IEnumerable<string>> GetDistinctCategoriesAsync();
    }
}
