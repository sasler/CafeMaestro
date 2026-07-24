namespace CafeMaestro.Services;

public readonly record struct DocumentSaveResult(
    bool IsSuccessful,
    bool IsCanceled,
    string? Location = null,
    Exception? Exception = null);

public interface IDocumentSaveService
{
    Task<DocumentSaveResult> SaveAsync(
        string suggestedFileName,
        string mimeType,
        Stream content,
        CancellationToken cancellationToken = default);
}
