using Microsoft.AspNetCore.Http;

namespace DocumentSharingWebApp.Services;

public interface IDocumentStorage
{
    Task<(string RelativePath, long SizeBytes, string OriginalName)>
        SaveAsync(IFormFile file, string subFolder, CancellationToken ct = default);
}
