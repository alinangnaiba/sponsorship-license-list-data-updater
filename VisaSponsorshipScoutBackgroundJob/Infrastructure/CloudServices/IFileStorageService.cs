namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices
{
    internal interface IFileStorageService
    {
        byte[]? Download(string bucket, string filename);
        void Upload(string bucket, string fileName, byte[] data);
    }
}
