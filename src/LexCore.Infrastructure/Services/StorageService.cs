using Amazon.S3;
using Amazon.S3.Model;
using LexCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class StorageService : IStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageService> _logger;
    private readonly IAmazonS3? _s3Client;
    private readonly bool _useLocalStorage;
    private readonly string _localStoragePath;
    private readonly string _bucketName;

    public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _useLocalStorage = configuration["Storage:UseLocal"]?.ToLower() == "true";
        _localStoragePath = configuration["Storage:LocalPath"] ?? "./uploads";
        _bucketName = configuration["Storage:S3BucketName"] ?? "";

        if (!_useLocalStorage)
        {
            var accessKey = configuration["Storage:S3AccessKey"];
            var secretKey = configuration["Storage:S3SecretKey"];
            var region = configuration["Storage:S3Region"] ?? "ap-south-1";

            if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
            {
                _s3Client = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
            }
        }
        else
        {
            Directory.CreateDirectory(_localStoragePath);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder)
    {
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var path = $"{folder}/{uniqueFileName}";

        if (_useLocalStorage)
        {
            var fullPath = Path.Combine(_localStoragePath, folder);
            Directory.CreateDirectory(fullPath);

            var filePath = Path.Combine(fullPath, uniqueFileName);
            using var fileStreamOut = new FileStream(filePath, FileMode.Create);
            await fileStream.CopyToAsync(fileStreamOut);

            _logger.LogInformation("File uploaded locally: {Path}", filePath);
            return $"local://{path}";
        }
        else
        {
            if (_s3Client == null)
                throw new InvalidOperationException("S3 client not configured");

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = path,
                InputStream = fileStream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(request);
            _logger.LogInformation("File uploaded to S3: {Path}", path);
            return $"s3://{_bucketName}/{path}";
        }
    }

    public async Task<Stream?> DownloadFileAsync(string fileUrl)
    {
        if (fileUrl.StartsWith("local://"))
        {
            var relativePath = fileUrl.Replace("local://", "");
            var fullPath = Path.Combine(_localStoragePath, relativePath);

            if (File.Exists(fullPath))
            {
                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            }
            return null;
        }
        else if (fileUrl.StartsWith("s3://"))
        {
            if (_s3Client == null)
                throw new InvalidOperationException("S3 client not configured");

            var path = fileUrl.Replace($"s3://{_bucketName}/", "");
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = path
            };

            var response = await _s3Client.GetObjectAsync(request);
            return response.ResponseStream;
        }

        return null;
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            if (fileUrl.StartsWith("local://"))
            {
                var relativePath = fileUrl.Replace("local://", "");
                var fullPath = Path.Combine(_localStoragePath, relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            else if (fileUrl.StartsWith("s3://"))
            {
                if (_s3Client == null)
                    throw new InvalidOperationException("S3 client not configured");

                var path = fileUrl.Replace($"s3://{_bucketName}/", "");
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = path
                };

                await _s3Client.DeleteObjectAsync(request);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FileUrl}", fileUrl);
            return false;
        }
    }

    public string GetPublicUrl(string fileUrl)
    {
        if (fileUrl.StartsWith("local://"))
        {
            var relativePath = fileUrl.Replace("local://", "");
            return $"/files/{relativePath}";
        }
        else if (fileUrl.StartsWith("s3://"))
        {
            var path = fileUrl.Replace($"s3://{_bucketName}/", "");
            var region = _configuration["Storage:S3Region"] ?? "ap-south-1";
            return $"https://{_bucketName}.s3.{region}.amazonaws.com/{path}";
        }

        return fileUrl;
    }

    public async Task<string> GetSignedUrlAsync(string fileUrl, int expiryMinutes = 60)
    {
        if (fileUrl.StartsWith("local://"))
        {
            // For local storage — return a token-based URL
            // The token encodes the path and expiry — verified in DownloadDocument endpoint
            var relativePath = fileUrl.Replace("local://", "");
            var expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds();
            // Simple HMAC token for local dev — not for production use
            var tokenData = $"{relativePath}:{expiry}";
            var secret = _configuration["Storage:LocalSigningSecret"] ?? "dev-secret-change-in-prod";
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = Convert.ToBase64String(
                hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(tokenData)));
            var safeHash = hash.Replace("+", "-").Replace("/", "_").Replace("=", "");
            return $"/api/documents/serve?path={Uri.EscapeDataString(relativePath)}&exp={expiry}&sig={safeHash}";
        }
        else if (fileUrl.StartsWith("s3://"))
        {
            if (_s3Client == null)
                throw new InvalidOperationException("S3 client not configured");

            var path = fileUrl.Replace($"s3://{_bucketName}/", "");

            var request = new Amazon.S3.Model.GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = path,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Verb = Amazon.S3.HttpVerb.GET
            };

            // GetPreSignedURL is synchronous in AWSSDK — wrap in Task
            var signedUrl = await Task.FromResult(_s3Client.GetPreSignedURL(request));
            return signedUrl;
        }

        return fileUrl;
    }
}
