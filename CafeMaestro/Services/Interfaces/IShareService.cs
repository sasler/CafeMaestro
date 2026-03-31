namespace CafeMaestro.Services;

public interface IShareService
{
    Task ShareFileAsync(string filePath, string title);
}
