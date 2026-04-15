namespace LexCore.Application.Interfaces;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder);
    Task<Stream?> DownloadFileAsync(string fileUrl);
    Task<bool> DeleteFileAsync(string fileUrl);
    string GetPublicUrl(string fileUrl);                        // keep — used for non-sensitive files
    Task<string> GetSignedUrlAsync(string fileUrl, int expiryMinutes = 60); // NEW — for documents
}
