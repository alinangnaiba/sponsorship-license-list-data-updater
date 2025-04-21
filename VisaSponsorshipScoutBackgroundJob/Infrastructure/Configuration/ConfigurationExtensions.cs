using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using System.Security.Cryptography.X509Certificates;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection ConfigureDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var applicationSettings = GetSettings(configuration);

            var store = CreateDocumentStore(applicationSettings);

            // Add last updated at in metadata
            store.OnBeforeStore += (sender, eventArgs) =>
            {
                var metadata = eventArgs.DocumentMetadata;
                metadata["Last-Updated-At"] = DateTime.UtcNow.ToString("o");
            };

            new Organisation_ByName().Execute(store);
            new ProcessLog_ByName().Execute(store);

            services.AddSingleton<IDocumentStore>(store);

            return services;
        }

        private static DocumentStore CreateDocumentStore(ApplicationSettings applicationSettings)
        {
            if (applicationSettings is null || applicationSettings.FileStorage is null || applicationSettings.DatabaseSettings is null)
            {
                throw new InvalidOperationException("Database settings are missing");
            }

            DocumentStore store = new()
            {
                Urls = applicationSettings.DatabaseSettings.Urls,
                Database = applicationSettings.DatabaseSettings.Database,
            };
            GoogleCloudStorageService googleCloudStorageService = new();
            if (!string.IsNullOrEmpty(applicationSettings.FileStorage.CertificateFilename))
            {
                var bytes = googleCloudStorageService.Download(applicationSettings.FileStorage.Bucket, $"cert/{applicationSettings.FileStorage.CertificateFilename}") ?? throw new InvalidOperationException("Certificate not found");
                store.Certificate = X509CertificateLoader.LoadPkcs12(bytes.ToArray(), null);
            }

            store.Initialize();
            EnsureDatabaseExists(store, applicationSettings.DatabaseSettings.Database);

            return store;
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
                throw new Exception($"Failed to create/verify database: {dbName}", ex);
            }
        }

        private static ApplicationSettings GetSettings(IConfiguration configuration)
        {
            var applicationSettings = new ApplicationSettings();
            var databaseConfig = configuration.GetSection(nameof(DatabaseSettings));
            var fileStorageConfig = configuration.GetSection(nameof(FileStorageSettings));
            applicationSettings.DatabaseSettings = databaseConfig.Get<DatabaseSettings>();
            applicationSettings.FileStorage = fileStorageConfig.Get<FileStorageSettings>();
            if (applicationSettings.DatabaseSettings is null || applicationSettings.FileStorage is null)
            {
                throw new InvalidOperationException("Database settings are missing");
            }
            return applicationSettings;
        }
    }
}