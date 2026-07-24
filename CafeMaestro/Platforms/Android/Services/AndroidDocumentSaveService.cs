using Android.App;
using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro.Services;

public sealed class AndroidDocumentSaveService : IDocumentSaveService
{
    private const int SaveDocumentRequestCode = 9142;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public async Task<DocumentSaveResult> SaveAsync(
        string suggestedFileName,
        string mimeType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        EventHandler<AndroidActivityResultEventArgs>? handler = null;

        try
        {
            var completion =
                new TaskCompletionSource<AndroidActivityResultEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            handler = (_, args) =>
            {
                if (args.RequestCode == SaveDocumentRequestCode)
                {
                    completion.TrySetResult(args);
                }
            };
            MainActivity.ActivityResultReceived += handler;

            using CancellationTokenRegistration registration =
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Activity activity = Platform.CurrentActivity ??
                                    throw new InvalidOperationException(
                                        "No Android activity is available for saving the document.");
                var intent = new Intent(Intent.ActionCreateDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType(mimeType);
                intent.PutExtra(Intent.ExtraTitle, suggestedFileName);
                activity.StartActivityForResult(intent, SaveDocumentRequestCode);
            });

            AndroidActivityResultEventArgs result = await completion.Task;
            if (result.ResultCode != Result.Ok || result.Data?.Data is null)
            {
                return new DocumentSaveResult(false, true);
            }

            Activity currentActivity = Platform.CurrentActivity ??
                                       throw new InvalidOperationException(
                                           "No Android activity is available to write the document.");
            await using Stream destination =
                currentActivity.ContentResolver?.OpenOutputStream(result.Data.Data, "wt") ??
                throw new IOException("Android did not provide a writable document stream.");
            await content.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);

            return new DocumentSaveResult(
                true,
                false,
                result.Data.Data.ToString());
        }
        catch (OperationCanceledException)
        {
            return new DocumentSaveResult(false, true);
        }
        catch (Exception ex)
        {
            return new DocumentSaveResult(false, false, Exception: ex);
        }
        finally
        {
            if (handler is not null)
            {
                MainActivity.ActivityResultReceived -= handler;
            }

            _saveLock.Release();
        }
    }
}
