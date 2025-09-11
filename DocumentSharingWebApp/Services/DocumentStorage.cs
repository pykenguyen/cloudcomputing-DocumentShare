using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace DocumentSharingWebApp.Services;

public class DocumentStorage : IDocumentStorage
{
    private readonly IWebHostEnvironment _env;
    private static readonly Regex Invalid = new("[^a-zA-Z0-9-_\\.]", RegexOptions.Compiled);

    public DocumentStorage(IWebHostEnvironment env) => _env = env;

    public async Task<(string RelativePath, long SizeBytes, string OriginalName)>
        SaveAsync(IFormFile file, string subFolder, CancellationToken ct = default)
    {
        var webroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var targetDir = Path.Combine(webroot, subFolder);
        Directory.CreateDirectory(targetDir);

        var safe = Invalid.Replace(Path.GetFileName(file.FileName), "_");
        var unique = $"{Path.GetFileNameWithoutExtension(safe)}_{Guid.NewGuid():N}{Path.GetExtension(safe)}";
        var abs = Path.Combine(targetDir, unique);

        await using (var fs = new FileStream(abs, FileMode.CreateNew))
            await file.CopyToAsync(fs, ct);

        var rel = Path.GetRelativePath(Path.Combine(webroot, "uploads"), abs).Replace('\\', '/');
        return (rel, file.Length, safe);
    }
}
