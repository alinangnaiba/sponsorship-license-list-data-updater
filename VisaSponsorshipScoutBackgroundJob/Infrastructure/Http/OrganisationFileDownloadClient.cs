namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Http
{
    public interface IFileDownloadClient
    {
        Task<byte[]?> DownloadFileAsByteArrayAsync(string fileUrl);
    }

    public class OrganisationFileDownloadClient : IFileDownloadClient
    {
        private readonly IHttpClientFactory _clientFactory;

        public OrganisationFileDownloadClient(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<byte[]?> DownloadFileAsByteArrayAsync(string fileUrl)
        {
            HttpClient client = _clientFactory.CreateClient();

            using HttpResponseMessage response = await client.GetAsync(fileUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
