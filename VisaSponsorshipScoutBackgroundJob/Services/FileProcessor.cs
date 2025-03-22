using Microsoft.Extensions.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;

namespace VisaSponsorshipScoutBackgroundJob.Services
{
    internal interface IFileProcessor
    {
        Task ProcessAsync();
    }

    internal class FileProcessor : IFileProcessor
    {
        private readonly IFileService _fileService;
        private readonly IConfiguration _configuration;
        private readonly IFileDownloadClient _fileDownloadClient;
        private readonly IWebScraper _webScraper;
        private const string FileLink = @"https://www.gov.uk/government/publications/register-of-licensed-sponsors-workers";

        public FileProcessor(IConfiguration configuration, IFileService fileService, IWebScraper webScraper, IFileDownloadClient fileDownloadClient)
        {
            _fileService = fileService;
            _configuration = configuration;
            _fileDownloadClient = fileDownloadClient;
            _webScraper = webScraper;
        }

        public async Task ProcessAsync()
        {
            DatabaseService databaseService = DatabaseService.Create(_configuration.GetSection(nameof(DatabaseSettings)).Get<DatabaseSettings>(), _fileService);
            ProcessLog newProcessLog = new()
            {
                StartedAt = DateTime.UtcNow,
                Status = ProcessStatus.InProgress
            };
            var newFileContent = await FetchNewFiles(newProcessLog, databaseService);
            if (newFileContent is null)
            {
                return;
            }
            string fileBucket = _configuration.GetSection(nameof(FileStorageSettings)).Get<FileStorageSettings>()?.OrganisationFileBucket ?? 
                throw new InvalidOperationException($"{nameof(FileStorageSettings)} not configured.");
            await _fileService.UploadFileAsync(fileBucket, newFileContent, databaseService);

            return;
        }

        private async Task<byte[]?>  FetchNewFiles(ProcessLog newProcessLog, DatabaseService databaseService)
        {
            var existingProcessLog = await databaseService.GetExistingInProgress();
            if (existingProcessLog is null)
            {
                await databaseService.SaveProcessLogAsync(newProcessLog);
            }
            else
            {
                existingProcessLog.Status = ProcessStatus.Failed;
                existingProcessLog.ErrorMessage = "Update unsuccessful.";
                await databaseService.SaveProcessLogAsync(existingProcessLog);
            }
            try
            {
                string? fileUrl = await _webScraper.ScrapeAttachmentLinkAsync(FileLink) ?? throw new InvalidOperationException("File link not found.");
                Uri uri = new(fileUrl);
                string fileName = Path.GetFileName(uri.LocalPath);
                newProcessLog.FileName = fileName;

                byte[] file = await _fileDownloadClient.DownloadFileAsByteArrayAsync(fileUrl);
                if (file is null)
                {
                    newProcessLog.Status = ProcessStatus.Failed;
                    newProcessLog.ErrorMessage = $"File download failed - URL: {fileUrl}.";

                    return file;
                }
                await databaseService.SaveProcessLogAsync(newProcessLog);
                return file;
            }
            catch (Exception ex)
            {
                newProcessLog.Status = ProcessStatus.Failed;
                newProcessLog.ErrorMessage = $"{nameof(FetchNewFiles)}: {ex.Message}";
                await databaseService.SaveProcessLogAsync(newProcessLog);
                throw;
            }
        }
    }
}
