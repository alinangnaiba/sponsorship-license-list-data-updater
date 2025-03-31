using Microsoft.Extensions.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;

namespace VisaSponsorshipScoutBackgroundJob.Services
{
    public interface IOrganisationFileService
    {
        Task<byte[]?> DownloadFromSourceAsync(string url, ProcessLog processLog);

        byte[]? DownloadFromStorage(ProcessLog processLog);

        void UploadToStorage(byte[] contents, ProcessLog filename);
    }

    public class OrganisationFileService : IOrganisationFileService
    {
        private const string OrganisationFileFolder = "org-files";

        private readonly IFileDownloadClient _fileDownloadClient;
        private readonly IFileStorageService _fileStorageService;
        private readonly FileStorageSettings _settings;

        public OrganisationFileService(IConfiguration configuration, IFileStorageService fileStorageService, IFileDownloadClient fileDownloadClient)
        {
            _fileDownloadClient = fileDownloadClient;
            var fileStorageConfig = configuration.GetSection(nameof(FileStorageSettings));
            _fileStorageService = fileStorageService;
            _settings = fileStorageConfig.Get<FileStorageSettings>() ?? throw new ArgumentNullException(nameof(fileStorageConfig));
        }

        public async Task<byte[]?> DownloadFromSourceAsync(string url, ProcessLog processLog)
        {
            try
            {
                byte[]? content = await _fileDownloadClient.DownloadFileAsByteArrayAsync(url);
                if (content is null)
                {
                    processLog.Status = ProcessStatus.Failed;
                    processLog.Errors.Add(new($"File download failed - URL: {url}."));
                    return null;
                }
                return content;
            }
            catch (Exception ex)
            {
                processLog.Status = ProcessStatus.Failed;
                processLog.Errors.Add(new(ex.Message, nameof(DownloadFromSourceAsync), ex.StackTrace));
                return null;
            }
        }

        public byte[]? DownloadFromStorage(ProcessLog processLog)
        {
            try
            {
                return _fileStorageService.Download(_settings.Bucket, $"{OrganisationFileFolder}/{processLog.FileName}");
            }
            catch (Exception ex)
            {
                processLog.Errors.Add(new(ex.Message, nameof(DownloadFromStorage), ex.StackTrace));
                processLog.Status = ProcessStatus.Failed;
                return null;
            }
        }

        public void UploadToStorage(byte[] contents, ProcessLog processLog)
        {
            try
            {
                string filename = $"{OrganisationFileFolder}/{processLog.FileName}";
                if (!_fileStorageService.FileExists(_settings.Bucket, filename))
                {
                    _fileStorageService.Upload(_settings.Bucket, filename, contents);
                }
            }
            catch (Exception ex)
            {
                processLog.Errors.Add(new(ex.Message, nameof(UploadToStorage), ex.StackTrace));
                processLog.Status = ProcessStatus.Failed;
            }
        }
    }
}