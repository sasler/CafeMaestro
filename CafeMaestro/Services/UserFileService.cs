using Microsoft.Maui.Storage;

namespace CafeMaestro.Services;

public sealed class UserFileService : IUserFileService
{
    private readonly IFilePicker _filePicker;
    private readonly IDocumentSaveService _documentSaveService;
    private readonly string _temporaryDirectory;
    private readonly Func<FileResult, Task<Stream>> _openReadAsync;

    public UserFileService(
        IFilePicker filePicker,
        IDocumentSaveService documentSaveService)
        : this(
            filePicker,
            documentSaveService,
            Path.Combine(FileSystem.CacheDirectory, "CafeMaestroImports"))
    {
    }

    public UserFileService(
        IFilePicker filePicker,
        IDocumentSaveService documentSaveService,
        string temporaryDirectory)
        : this(filePicker, documentSaveService, temporaryDirectory, result => result.OpenReadAsync())
    {
    }

    public UserFileService(
        IFilePicker filePicker,
        IDocumentSaveService documentSaveService,
        string temporaryDirectory,
        Func<FileResult, Task<Stream>> openReadAsync)
    {
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _documentSaveService = documentSaveService ??
                               throw new ArgumentNullException(nameof(documentSaveService));
        _temporaryDirectory = string.IsNullOrWhiteSpace(temporaryDirectory)
            ? throw new ArgumentException("Temporary directory is required.", nameof(temporaryDirectory))
            : temporaryDirectory;
        _openReadAsync = openReadAsync ?? throw new ArgumentNullException(nameof(openReadAsync));
    }

    public async Task<UserFileSelection?> PickFileAsync(
        UserFileType fileType,
        string pickerTitle,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FileResult? result = await _filePicker.PickAsync(new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = CreateFileTypes(fileType)
        });

        if (result is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_temporaryDirectory);

        string displayName = string.IsNullOrWhiteSpace(result.FileName)
            ? $"import.{GetExtension(fileType)}"
            : Path.GetFileName(result.FileName);
        string safeFileName = string.Concat(
            displayName.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        string localPath = Path.Combine(
            _temporaryDirectory,
            $"{Guid.NewGuid():N}_{safeFileName}");

        try
        {
            await using Stream source = await _openReadAsync(result);
            await using var destination = new FileStream(
                localPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);
            await source.CopyToAsync(destination, cancellationToken);
            return new UserFileSelection(displayName, localPath);
        }
        catch
        {
            DeleteTemporaryFile(localPath);
            throw;
        }
    }

    public Task<DocumentSaveResult> SaveFileAsync(
        string suggestedFileName,
        string mimeType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        return _documentSaveService.SaveAsync(
            suggestedFileName,
            mimeType,
            content,
            cancellationToken);
    }

    public void DeleteTemporaryFile(string? localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        string resolvedPath = Path.GetFullPath(localPath);
        string resolvedTemporaryDirectory =
            Path.GetFullPath(_temporaryDirectory) + Path.DirectorySeparatorChar;
        if (!resolvedPath.StartsWith(
                resolvedTemporaryDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (File.Exists(resolvedPath))
            {
                File.Delete(resolvedPath);
            }
        }
        catch (IOException)
        {
            // Cache cleanup is best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Cache cleanup is best-effort.
        }
    }

    private static FilePickerFileType CreateFileTypes(UserFileType fileType)
    {
        return fileType switch
        {
            UserFileType.Json => new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".json"] },
                    { DevicePlatform.Android, ["application/json", "text/json", "*/*"] },
                    { DevicePlatform.iOS, ["public.json"] },
                    { DevicePlatform.MacCatalyst, ["public.json"] }
                }),
            _ => new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".csv"] },
                    {
                        DevicePlatform.Android,
                        ["text/csv", "text/comma-separated-values", "application/csv", "*/*"]
                    },
                    { DevicePlatform.iOS, ["public.comma-separated-values-text"] },
                    { DevicePlatform.MacCatalyst, ["public.comma-separated-values-text"] }
                })
        };
    }

    private static string GetExtension(UserFileType fileType) =>
        fileType == UserFileType.Json ? "json" : "csv";
}
