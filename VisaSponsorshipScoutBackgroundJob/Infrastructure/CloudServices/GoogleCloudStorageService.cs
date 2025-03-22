using Google.Cloud.Storage.V1;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices
{
    internal class GoogleCloudStorageService : IFileStorageService
    {
        private const string ContentType = "application/octet-stream";

        public byte[]? Download(string bucket, string filename)
        {
            StorageClient storage = StorageClient.Create();
            MemoryStream stream = new();
            storage.DownloadObject(bucket, filename, stream);

            return stream.ToArray();
        }

        public void Upload(string bucket, string fileName, byte[] contents)
        {
            StorageClient storage = StorageClient.Create();
            MemoryStream stream = new(contents);
            storage.UploadObject(bucket, fileName, ContentType, stream);
        }
    }
}
