using CsvHelper;
using CsvHelper.Configuration;
using Raven.Client.Documents;
using Raven.Client.Documents.BulkInsert;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Session;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Core.Extensions;
using VisaSponsorshipScoutBackgroundJob.Core.Model;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;

namespace VisaSponsorshipScoutBackgroundJob.Services
{
    public interface IFileProcessor
    {
        Task ProcessAsync();
    }

    //TODO: Add unit tests for this class
    // Need to refactor the class to make it more testable
    // and to remove the dependency on the RavenDB document store.
    // We need to make a new class(Repository?) that will handle the
    // database operations and inject it into this class.
    // We need to think about how to handle the RavenDB session.
    public class FileProcessor : IFileProcessor
    {
        private const int BatchSize = 5000;
        private const int MaxParallel = 4;

        private readonly ICrawler _crawler;
        private readonly IDocumentStore _documentStore;
        private readonly IOrganisationFileService _fileService;

        public FileProcessor(IDocumentStore documentStore, IOrganisationFileService fileService, ICrawler webScraper)
        {
            _fileService = fileService;
            _crawler = webScraper;
            _documentStore = documentStore;
        }

        private List<Organisation> ExistingOrganisations { get; set; } = [];

        private byte[]? FileContent { get; set; } = null;

        private ProcessLog ProcessLog { get; set; }

        public async Task ProcessAsync()
        {
            using IAsyncDocumentSession session = _documentStore.OpenAsyncSession();
            ProcessLog = await GetExistingOrCreateProcessLog(session);
            try
            {
                string? sourceLastUpdateString = await _crawler.ScrapeLastUpdatedDateAsync(ProcessLog);
                if (sourceLastUpdateString is null)
                {
                    return;
                }
                if (!HasUpdateFromSource(sourceLastUpdateString))
                {
                    ProcessLog.Status = ProcessStatus.NoUpdate;
                    return;
                }
                ProcessLog.SourceLastUpdate = DateTime.Parse(sourceLastUpdateString);
                await Task.WhenAll(
                    FetchContentFromSourceOrStorageAsync(),
                    LoadExistingOrganisationsAsync(session)
                    );

                if (FileContent is null)
                {
                    return;
                }

                if (FileContent.Length > 0)
                {
                    _fileService.UploadToStorage(FileContent, ProcessLog);
                    if (ProcessLog.Status == ProcessStatus.Failed)
                    {
                        return;
                    }
                }

                await ProcessFileContentAsync();
            }
            catch (Exception ex)
            {
                ProcessLog.Status = ProcessStatus.Failed;
                ProcessLog.Errors.Add(new(ex.Message, nameof(ProcessAsync), ex.StackTrace));
            }
            finally
            {
                //save process log
                if (ProcessLog.Status != ProcessStatus.NoUpdate)
                {
                    await session.SaveChangesAsync();
                }
            }
        }

        private static async Task<ProcessLog> GetExistingOrCreateProcessLog(IAsyncDocumentSession session)
        {
            var processLog = await session.Query<ProcessLog>()
                .Where(log => log.Status == ProcessStatus.InProgress)
                .FirstOrDefaultAsync() ??
                new()
                {
                    StartedAt = DateTime.UtcNow,
                    Status = ProcessStatus.InProgress
                };
            if (string.IsNullOrEmpty(processLog.Id))
            {
                await session.StoreAsync(processLog);
            }
            return processLog;
        }

        private static bool NeedsUpdate(Organisation org, Organisation existingOrg)
        {
            return org.County != existingOrg.County
                || !org.TownCities.IsEqualTo(existingOrg.TownCities)
                || !org.TypeAndRatings.IsEqualTo(existingOrg.TypeAndRatings)
                || !org.Routes.IsEqualTo(existingOrg.Routes);
        }

        private static void ReadFile(byte[] fileContent, List<Organisation> organisations)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                HeaderValidated = null,
                MissingFieldFound = null
            };
            using var reader = new StreamReader(new MemoryStream(fileContent));
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<OrganisationMap>();
            var models = new List<OrganisationModel>();
            while (csv.Read())
            {
                models.Add(csv.GetRecord<OrganisationModel>());
            }

            organisations.AddRange(models
                .GroupBy(org => org.Name.Trim())
                .Select(group => new Organisation
                {
                    Name = group.Key.Trim(),
                    TownCities = group.Select(o => o.TownCity.Trim()).Distinct().ToList(),
                    County = group.First().County.Trim(),
                    TypeAndRatings = group.Select(o => o.TypeAndRating.Trim()).Distinct().ToList(),
                    Routes = group.Select(o => o.Route.Trim()).Distinct().ToList()
                })
                .ToList());
        }

        private RecordDetail CreateRecordDetail(Organisation organisation)
        {
            return new RecordDetail(organisation.Name, organisation.County, organisation.TownCities, organisation.TypeAndRatings, organisation.Routes);
        }

        private async Task FetchContentFromSourceOrStorageAsync()
        {
            try
            {
                string? fileUrl = await _crawler.ScrapeAttachmentLinkAsync(ProcessLog) ?? throw new InvalidOperationException("File link not found.");
                Uri uri = new(fileUrl);
                string fileName = Path.GetFileName(uri.LocalPath);

                // FileName should be empty for new process log. Otherwise, it's an existing process
                // that's not completed successfully. Process it again and we should check for the
                // content from storage and download it.
                if (ProcessLog.FileName == fileName)
                {
                    ProcessLog.StartedAt = DateTime.UtcNow;
                    var contentFromStorage = _fileService.DownloadFromStorage(ProcessLog);
                    if (contentFromStorage is not null)
                    {
                        FileContent = contentFromStorage;
                        return;
                    }
                }

                ProcessLog.FileName = fileName;
                FileContent = await _fileService.DownloadFromSourceAsync(fileUrl, ProcessLog);

                return;
            }
            catch (Exception ex)
            {
                ProcessLog.Status = ProcessStatus.Failed;
                ProcessLog.Errors.Add(new(ex.Message, nameof(FetchContentFromSourceOrStorageAsync), ex.StackTrace));
                return;
            }
        }

        private bool HasUpdateFromSource(string? sourceLastUpdateString)
        {
            IDocumentSession session = _documentStore.OpenSession();
            var lastProcessLog = session.Query<ProcessLog>()
                .Where(log => log.Status == ProcessStatus.Completed)
                .OrderByDescending(log => log.FinishedAt)
                .FirstOrDefault();

            if (lastProcessLog == null)
            {
                return true;
            }

            //assume the DateTime from the source is same as the server
            if (DateTime.TryParse(sourceLastUpdateString, out DateTime sourceLastUpdate))
            {
                DateTime finishedAtUtc = lastProcessLog.FinishedAt?.ToUniversalTime() ?? DateTime.MinValue.ToUniversalTime();
                DateTime sourceLastUpdateUtc = DateTime.SpecifyKind(sourceLastUpdate, DateTimeKind.Unspecified).ToUniversalTime();

                // Compare the UTC times.
                return sourceLastUpdateUtc > finishedAtUtc;
            }

            return false;
        }

        private async Task InsertAsync(List<Organisation> batch)
        {
            if (batch.Count == 0)
            {
                return;
            }

            using BulkInsertOperation bulkInsert = _documentStore.BulkInsert();
            foreach (var org in batch)
            {
                org.Id = Guid.NewGuid().ToString();
                await bulkInsert.StoreAsync(org);
            }
        }

        private async Task LoadExistingOrganisationsAsync(IAsyncDocumentSession session)
        {
            var query = session.Query<Organisation>();
            await using var stream = await session.Advanced.StreamAsync(query);
            while (await stream.MoveNextAsync())
            {
                var entity = stream.Current.Document;
                ExistingOrganisations.Add(entity);
            }
        }

        private void PopulateQueue(List<Organisation> organisationsFromCsv, ConcurrentDictionary<string, Organisation> existingDictionary, ConcurrentQueue<Organisation> organisationQueue)
        {
            foreach (var org in organisationsFromCsv)
            {
                if (!existingDictionary.TryGetValue(org.Name, out Organisation? existingOrg))
                {
                    organisationQueue.Enqueue(org);
                }
                else if (NeedsUpdate(org, existingOrg))
                {
                    ProcessLog.UpdatedRecords.UpdateRecordDetails.Add(new UpdateRecordDetail(CreateRecordDetail(existingOrg), CreateRecordDetail(org)));
                    existingOrg.TownCities = org.TownCities;
                    existingOrg.County = org.County.Trim();
                    existingOrg.TypeAndRatings = org.TypeAndRatings;
                    existingOrg.Routes = org.Routes;
                }
            }
        }

        private async Task ProcessBatchAsync(ConcurrentQueue<Organisation> organisationQueue, SemaphoreSlim semaphore)
        {
            try
            {
                var batch = new List<Organisation>();
                var items = 0;
                while (items < BatchSize && organisationQueue.TryDequeue(out var org))
                {
                    batch.Add(org);
                    ProcessLog.AddedRecords.OrganisationNames.Add(org.Name);
                    items++;
                }

                await InsertAsync(batch);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task ProcessFileContentAsync()
        {
            if (FileContent is null)
            {
                ProcessLog.Status = ProcessStatus.Failed;
                ProcessLog.Errors.Add(new("File content is null."));
                return;
            }
            var stopwatch = new Stopwatch();
            Console.WriteLine("Processing file started...");
            stopwatch.Start();
            using IAsyncDocumentSession session = _documentStore.OpenAsyncSession();
            SemaphoreSlim semaphore = new(MaxParallel);
            ConcurrentQueue<Organisation> organisationQueue = new();
            List<Organisation> organisationsFromCsv = [];
            ReadFile(FileContent, organisationsFromCsv);

            SendDeleteOrganisationCommand(ExistingOrganisations, organisationsFromCsv, session, out int orgsToDeleteCount);

            var existingOrgDictionary = ExistingOrganisations.ToConcurrentDictionary(org => org.Name);
            var readTask = Task.Run(() => PopulateQueue(organisationsFromCsv, existingOrgDictionary, organisationQueue));
            List<Task> tasks = [];

            while (!readTask.IsCompleted || !organisationQueue.IsEmpty)
            {
                if (organisationQueue.Count >= BatchSize || (readTask.IsCompleted && !organisationQueue.IsEmpty))
                {
                    await semaphore.WaitAsync();
                    tasks.Add(ProcessBatchAsync(organisationQueue, semaphore));
                }
            }
            await Task.WhenAll(tasks);
            ProcessLog.Status = ProcessStatus.Completed;
            ProcessLog.FinishedAt = DateTime.UtcNow;
            ProcessLog.TotalRecordsProcessed = organisationsFromCsv.Count;

            await session.SaveChangesAsync();
            stopwatch.Stop();
            Console.WriteLine($"Processing file completed in {stopwatch.Elapsed}");
        }

        private void SendDeleteOrganisationCommand(List<Organisation> existingOrgs, List<Organisation> organisationsFromCsv, IAsyncDocumentSession session, out int orgsToDeleteCount)
        {
            List<string> orgNamesToBeDeleted = existingOrgs.Select(org => org.Name).Except(organisationsFromCsv.Select(org => org.Name)).ToList();
            List<Organisation> orgsToBeDeleted = existingOrgs.Where(org => orgNamesToBeDeleted.Contains(org.Name)).ToList();
            orgsToDeleteCount = orgsToBeDeleted.Count;
            foreach (var org in orgsToBeDeleted)
            {
                session.Advanced.Defer(new DeleteCommandData(org.Id, null));
            }
            existingOrgs.RemoveAll(org => orgNamesToBeDeleted.Contains(org.Name));
            ProcessLog.DeletedRecords = new DeletedOrganisations(orgNamesToBeDeleted.Count, orgNamesToBeDeleted);
        }
    }
}