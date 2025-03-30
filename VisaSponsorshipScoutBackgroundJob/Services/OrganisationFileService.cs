using Microsoft.Extensions.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;

namespace VisaSponsorshipScoutBackgroundJob.Services
{
    public interface IOrganisationFileService
    {
        byte[]? DownloadOrganisationFile(ProcessLog processLog);
        void UploadOrganisationFile(byte[] contents, ProcessLog filename);
    }

    public class OrganisationFileService : IOrganisationFileService
    {
        private readonly FileStorageSettings _settings;
        private readonly IFileStorageService _fileStorageService;
        private const string OrganisationFileFolder = "org-files";

        public OrganisationFileService(IConfiguration configuration, IFileStorageService fileStorageService)
        {
            var fileStorageConfig = configuration.GetSection(nameof(FileStorageSettings));
            _fileStorageService = fileStorageService;
            _settings = fileStorageConfig.Get<FileStorageSettings>() ?? throw new ArgumentNullException(nameof(fileStorageConfig));
        }

        public byte[]? DownloadOrganisationFile(ProcessLog processLog)
        {
            try
            {
                return _fileStorageService.Download(_settings.Bucket, $"{OrganisationFileFolder}/{processLog.FileName}");
            }
            catch (Exception ex)
            {
                processLog.ErrorMessage = ex.Message;
                processLog.Status = ProcessStatus.Failed;
                return null;
            }
        }

        public void UploadOrganisationFile(byte[] contents, ProcessLog processLog)
        {
            try
            {
                string filename = $"{OrganisationFileFolder}/{processLog.FileName}";
                if (!_fileStorageService.FileExists(_settings.Bucket, filename))
                {
                    _fileStorageService.Upload(_settings.Bucket, filename, contents);
                }
            }
            catch(Exception ex)
            {
                processLog.ErrorMessage = ex.Message;
                processLog.Status = ProcessStatus.Failed;                
            }
        }
    }
}
