using HtmlAgilityPack;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Http
{
    internal interface IWebScraper
    {
        Task<string?> ScrapeAttachmentLinkAsync(string url);
    }

    internal class WebScraper : IWebScraper
    {
        private IHttpClientFactory _clientFactory;

        internal WebScraper(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<string?> ScrapeAttachmentLinkAsync(string url)
        {
            try
            {
                HttpClient client = _clientFactory.CreateClient();
                string html = await client.GetStringAsync(url);

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var linkNode = doc.DocumentNode.Descendants("a")
                    .Where(node => node.GetAttributeValue("class", "").Contains("gem-c-attachment__link"))
                    .FirstOrDefault();

                if (linkNode != null)
                {
                    return linkNode.GetAttributeValue("href", "");
                }

                return null; // or throw an exception, or return empty string
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching URL: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return null;
            }
        }
    }
}
