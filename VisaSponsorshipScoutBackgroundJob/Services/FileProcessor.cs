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
        private readonly ICrawler _crawler;

        public FileProcessor(IConfiguration configuration, IFileService fileService, ICrawler webScraper, IFileDownloadClient fileDownloadClient)
        {
            _fileService = fileService;
            _configuration = configuration;
            _fileDownloadClient = fileDownloadClient;
            _crawler = webScraper;
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
            if (newFileContent.Length > 0)
            {
                await _fileService.UploadFileAsync(newFileContent, databaseService);
            }
            else
            {
                // File should already be in the storage.
                newFileContent = await _fileService.DownloadFileAsync(newProcessLog.FileName);
            }
            //TODO: Process the file content
            await ProcessFile(newProcessLog, newFileContent);
            return;
        }

        private async Task ProcessFile(ProcessLog newProcessLog, byte[] fileContent)
        {
            //TODO: Implement file processing logic
        }

        private async Task<byte[]?>  FetchNewFiles(ProcessLog newProcessLog, DatabaseService databaseService)
        {
            try
            {
                string? fileUrl = await _crawler.ScrapeAttachmentLinkAsync() ?? throw new InvalidOperationException("File link not found.");
                Uri uri = new(fileUrl);
                string fileName = Path.GetFileName(uri.LocalPath);

                var existingProcessLog = await databaseService.GetExistingInProgress();
                if (existingProcessLog is null)
                {
                    await databaseService.SaveProcessLogAsync(newProcessLog);
                }
                else if (existingProcessLog.FileName == fileName)
                {
                    newProcessLog = existingProcessLog;
                    newProcessLog.StartedAt = DateTime.UtcNow;
                    return [];
                }
                else
                {
                    existingProcessLog.Status = ProcessStatus.Failed;
                    existingProcessLog.ErrorMessage = "Update unsuccessful.";
                    await databaseService.SaveProcessLogAsync(existingProcessLog);
                }

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
                return null;
            }
        }
    }
}
