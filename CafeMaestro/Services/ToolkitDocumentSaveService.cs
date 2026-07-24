using CommunityToolkit.Maui.Storage;

namespace CafeMaestro.Services;

public sealed class ToolkitDocumentSaveService(IFileSaver fileSaver) : IDocumentSaveService
{
    private readonly IFileSaver _fileSaver =
        fileSaver ?? throw new ArgumentNullException(nameof(fileSaver));

    public async Task<DocumentSaveResult> SaveAsync(
        string suggestedFileName,
        string mimeType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            FileSaverResult result = await _fileSaver.SaveAsync(
                suggestedFileName,
                content,
                cancellationToken);

            if (result.IsSuccessful)
            {
                return new DocumentSaveResult(true, false, result.FilePath);
            }

            bool isCanceled = result.Exception is OperationCanceledException ||
                              result.Exception?.Message.Contains(
                                  "cancel",
                                  StringComparison.OrdinalIgnoreCase) == true;

            return new DocumentSaveResult(
                false,
                isCanceled,
                result.FilePath,
                result.Exception);
        }
        catch (OperationCanceledException)
        {
            return new DocumentSaveResult(false, true);
        }
        catch (Exception ex)
        {
            return new DocumentSaveResult(false, false, Exception: ex);
        }
    }
}
