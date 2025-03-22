using Raven.Client.Documents;
using Raven.Client.ServerWide.Operations;
using Raven.Client.ServerWide;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Core;

namespace VisaSponsorshipScoutBackgroundJob.Services
{
    internal interface IDatabaseService
    {
        Task<ProcessLog> GetLatestProcessLogAsync();
        Task<ProcessLog?> GetExistingInProgress();
        Task SaveProcessLogAsync(ProcessLog processLog);
    }

    internal class DatabaseService : IDatabaseService
    {
        private readonly IDocumentStore _documentStore;
        public DatabaseService(IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public async Task<ProcessLog?> GetLatestProcessLogAsync()
        {
            using var session = _documentStore.OpenAsyncSession();
            return await session.Query<ProcessLog>()
                .Where(log => log.Status == ProcessStatus.InProgress)
                .OrderByDescending(process => process.StartedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<ProcessLog?> GetExistingInProgress()
        {
            using var session = _documentStore.OpenAsyncSession();
            return await session.Query<ProcessLog>()
                .Where(log => log.Status == ProcessStatus.InProgress)
                .FirstOrDefaultAsync();
        }

        public async Task SaveProcessLogAsync(ProcessLog processLog)
        {
            using var session = _documentStore.OpenAsyncSession();
            await session.StoreAsync(processLog);
            await session.SaveChangesAsync();
        }

        internal static DatabaseService Create(DatabaseSettings databaseSettings, IFileService certificateService)
        {
            if (databaseSettings is null)
            {
                throw new InvalidOperationException("Database settings are missing");
            }
            var certificate = certificateService.GetCertificate();
            DocumentStore store = new()
            {
                Urls = databaseSettings.Urls,
                Database = databaseSettings.Database,
            };

            if (certificate is not null)
            {
                store.Certificate = certificate;
            }
            store.Initialize();
            EnsureDatabaseExists(store, databaseSettings.Database);

            return new DatabaseService(store);
        }

        private static void EnsureDatabaseExists(DocumentStore store, string dbName)
        {
            try
            {
                var db = store.Maintenance.Server.Send(new GetDatabaseRecordOperation(dbName));
                if (db is null)
                {
                    var createDbOperation = new CreateDatabaseOperation(new DatabaseRecord(dbName));
                    store.Maintenance.Server.Send(createDbOperation);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create/verify database: {dbName}", ex);
            }
        }

        internal IDocumentStore GetDocumentStore()
        {
            return _documentStore;
        }
    }
}
