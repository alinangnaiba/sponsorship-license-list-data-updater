using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Http
{
    internal interface ICrawler
    {
        Task<string?> ScrapeAttachmentLinkAsync();
    }

    internal class Crawler : ICrawler
    {
        private IHttpClientFactory _clientFactory;
        private CrawlerSettings _settings;

        internal Crawler(IConfiguration configuration, IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _settings = configuration.GetSection(nameof(CrawlerSettings)).Get<CrawlerSettings>() ?? throw new InvalidOperationException($"{nameof(CrawlerSettings)} not configured.");
        }

        public async Task<string?> ScrapeAttachmentLinkAsync()
        {
            try
            {
                HttpClient client = _clientFactory.CreateClient();
                string html = await client.GetStringAsync(_settings.CrawlUrl);

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var linkNode = doc.DocumentNode.Descendants("a")
                    .Where(node => node.GetAttributeValue("class", "").Contains("gem-c-attachment__link"))
                    .FirstOrDefault();

                if (linkNode != null)
                {
                    return linkNode.GetAttributeValue("href", "");
                }

                return null;
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
