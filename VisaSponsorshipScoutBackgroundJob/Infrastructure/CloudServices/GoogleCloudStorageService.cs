using Google;
using Google.Cloud.Storage.V1;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices
{
    public class GoogleCloudStorageService : IFileStorageService
    {
        private const string ContentType = "application/octet-stream";

        public byte[]? Download(string bucket, string filename)
        {
            if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(filename))
            {
                return null;
            }
            StorageClient storage = StorageClient.Create();
            MemoryStream stream = new();
            storage.DownloadObject(bucket, filename, stream);

            return stream.ToArray();
        }

        public bool FileExists(string bucket, string filename)
        {
            StorageClient storage = StorageClient.Create();
            try
            {
                storage.GetObject(bucket, filename);
                return true;
            }
            catch (GoogleApiException)
            {
                return false;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Upload(string bucket, string fileName, byte[] contents)
        {
            StorageClient storage = StorageClient.Create();
            MemoryStream stream = new(contents);
            storage.UploadObject(bucket, fileName, ContentType, stream);
        }
    }
}