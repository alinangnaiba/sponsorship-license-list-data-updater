using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Http
{
    public interface ICrawler
    {
        Task<string?> ScrapeAttachmentLinkAsync(ProcessLog processLog);

        Task<string?> ScrapeLastUpdatedDateAsync(ProcessLog processLog);
    }

    public class Crawler : ICrawler
    {
        private IHttpClientFactory _clientFactory;
        private CrawlerSettings _settings;

        public Crawler(IConfiguration configuration, IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _settings = configuration.GetSection(nameof(CrawlerSettings)).Get<CrawlerSettings>() ?? throw new InvalidOperationException($"{nameof(CrawlerSettings)} not configured.");
        }

        public async Task<string?> ScrapeAttachmentLinkAsync(ProcessLog processLog)
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
                processLog.Status = ProcessStatus.Failed;
                processLog.Errors.Add(new("Link node not found."));

                return null;
            }
            catch (Exception ex)
            {
                processLog.Errors.Add(new(ex.Message, nameof(ScrapeAttachmentLinkAsync), ex.StackTrace));
                processLog.Status = ProcessStatus.Failed;
                return null;
            }
        }

        public async Task<string?> ScrapeLastUpdatedDateAsync(ProcessLog processLog)
        {
            try
            {
                HttpClient client = _clientFactory.CreateClient();
                string html = await client.GetStringAsync(_settings.CrawlUrl);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                var dateNode = doc.DocumentNode.Descendants("time")
                    .Where(node => node.GetAttributeValue("class", "").Contains("gem-c-published-dates__change-date timestamp"))
                    .FirstOrDefault();
                if (dateNode == null)
                {
                    processLog.Status = ProcessStatus.Failed;
                    processLog.Errors.Add(new("Date node not found."));
                    return null;
                }
                return dateNode.Attributes["datetime"].Value; ;
            }
            catch (Exception ex)
            {
                processLog.Errors.Add(new(ex.Message, nameof(ScrapeLastUpdatedDateAsync), ex.StackTrace));
                processLog.Status = ProcessStatus.Failed;
                return null;
            }
        }
    }
}