namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices
{
    public interface IFileStorageService
    {
        byte[]? Download(string bucket, string filename);

        bool FileExists(string bucket, string filename);

        void Upload(string bucket, string fileName, byte[] data);
    }
}