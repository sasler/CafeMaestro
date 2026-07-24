namespace CafeMaestro.Services;

public enum UserFileType
{
    Json,
    Csv
}

public sealed record UserFileSelection(string DisplayName, string LocalPath);

public interface IUserFileService
{
    Task<UserFileSelection?> PickFileAsync(
        UserFileType fileType,
        string pickerTitle,
        CancellationToken cancellationToken = default);

    Task<DocumentSaveResult> SaveFileAsync(
        string suggestedFileName,
        string mimeType,
        Stream content,
        CancellationToken cancellationToken = default);

    void DeleteTemporaryFile(string? localPath);
}
