namespace CafeMaestro.Services;

public class ShareService : IShareService
{
    public async Task ShareFileAsync(string filePath, string title)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The file to share was not found.", filePath);
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath)
        });
    }
}
