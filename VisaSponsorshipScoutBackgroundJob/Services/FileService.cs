using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;

namespace VisaSponsorshipScoutBackgroundJob.Services
{
    internal interface IFileService
    {
        X509Certificate2? GetCertificate();
        Task UploadFileAsync(string bucket, byte[] contents, IDatabaseService databaseService);
    }

    internal class FileService : IFileService
    {
        private readonly FileStorageSettings _settings;
        private readonly IFileStorageService _fileStorageService;

        public FileService(IConfiguration configuration, IFileStorageService fileStorageService)
        {
            var fileStorageConfig = configuration.GetSection(nameof(FileStorageSettings));
            _fileStorageService = fileStorageService;
            _settings = fileStorageConfig.Get<FileStorageSettings>() ?? throw new ArgumentNullException(nameof(fileStorageConfig));
        }

        public X509Certificate2? GetCertificate()
        {
            byte[]? bytes = _fileStorageService.Download(_settings.CertificateBucket, _settings.FileName);
            if (bytes is null)
            {
                return null;
            }
            return X509CertificateLoader.LoadPkcs12(bytes.ToArray(), null);
        }

        public async Task UploadFileAsync(string bucket, byte[] contents, IDatabaseService databaseService)
        {
            var filename = ((await databaseService.GetExistingInProgress())?.FileName) ?? throw new InvalidOperationException("No in progress process.");
            _fileStorageService.Upload(bucket, filename, contents);
        }
    }
}
