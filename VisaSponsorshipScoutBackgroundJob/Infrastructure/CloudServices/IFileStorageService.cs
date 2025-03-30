namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices
{
    public interface IFileStorageService
    {
        bool FileExists(string bucket, string filename);
        byte[]? Download(string bucket, string filename);
        void Upload(string bucket, string fileName, byte[] data);
    }
}
