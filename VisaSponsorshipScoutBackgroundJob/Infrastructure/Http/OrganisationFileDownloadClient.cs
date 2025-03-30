namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Http
{
    internal interface IFileDownloadClient
    {
        Task<byte[]?> DownloadFileAsByteArrayAsync(string fileUrl);
    }

    internal class OrganisationFileDownloadClient : IFileDownloadClient
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
