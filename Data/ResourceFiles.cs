namespace Genealogy.Data;

public sealed class ResourceFiles(ClubStorage storage, ILogger<ResourceFiles> logger)
{
    public static string? ContentType(string kind, string fileName)
    {
        return (kind, Path.GetExtension(fileName).ToLowerInvariant()) switch
        {
            ("Slides", ".pdf") => "application/pdf",
            ("Slides", ".pptx") => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ("Video", ".mp4") => "video/mp4",
            ("Video", ".webm") => "video/webm",
            _ => null
        };
    }

    public async Task<string> StoreAsync(IFormFile upload, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        try
        {
            await using var output = new FileStream(storage.PathFor(key), FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous);
            await upload.CopyToAsync(output, cancellationToken);
            return key;
        }
        catch
        {
            Remove(key);
            throw;
        }
    }

    public FileStream? Open(string key)
    {
        try
        {
            return new FileStream(storage.PathFor(key), FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public void Remove(string? key)
    {
        if (key is null)
            return;

        try
        {
            File.Delete(storage.PathFor(key));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not remove unused resource file {StorageKey}.", key);
        }
    }
}
