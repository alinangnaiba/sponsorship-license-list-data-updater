namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration
{
    internal class ApplicationSettings
    {
        public FileStorageSettings FileStorage { get; set; } = new FileStorageSettings();
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();
    }

    internal class FileStorageSettings
    {
        public string CertificateBucket{ get; set; }
        public string FileName { get; set; }
        public string OrganisationFileBucket { get; set; }
    }

    internal class DatabaseSettings
    {
        public string Database { get; set; }
        public string[] Urls { get; set; }
    }
}
