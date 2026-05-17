using AutoNext.Plotform.App.Backoffice.Models.Blob;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Http;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Blob
{
    public interface IBlobService
    {
        Task<FileUploadResponseDto> UploadAsync(IFormFile file, Dictionary<string, string>? metadata = null);
        Task<bool> DeleteAsync(Guid fileId);
        Task<FileMetadata?> GetMetadataAsync(Guid fileId);
        Task<(Stream FileStream, string ContentType, string FileName)> DownloadAsync(Guid fileId);
        Task<List<FileMetadata>> ListFilesAsync(int page = 1, int pageSize = 50);
    }
}