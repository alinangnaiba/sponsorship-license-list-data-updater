namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration
{
    internal class ApplicationSettings
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();
        public FileStorageSettings FileStorage { get; set; } = new FileStorageSettings();
    }

    internal class CrawlerSettings
    {
        public string CrawlUrl { get; set; }
    }

    internal class DatabaseSettings
    {
        public string Database { get; set; }
        public string[] Urls { get; set; }
    }

    internal class FileStorageSettings
    {
        public string Bucket { get; set; }
        public string CertificateFilename { get; set; }
    }
}