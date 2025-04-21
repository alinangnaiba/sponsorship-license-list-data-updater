using Microsoft.Extensions.Configuration;
using System.Net;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;

namespace VisaSponsorshipScoutBackgroundJob.UnitTests;

public class CrawlerTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpMessageHandlerSubstitute _handlerSubstitute;
    private readonly IConfiguration _configuration;
    private readonly ProcessLog _processLog;

    public CrawlerTests()
    {
        _handlerSubstitute = new HttpMessageHandlerSubstitute();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        
        var httpClient = new HttpClient(_handlerSubstitute)
        {
            BaseAddress = new Uri("https://www.gov.uk")
        };
        
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(httpClient);

        var configurationValues = new Dictionary<string, string>
        {
            { "CrawlerSettings:CrawlUrl", "https://www.gov.uk/government/publications/register-of-licensed-sponsors-workers" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        _processLog = new ProcessLog
        {
            Status = ProcessStatus.InProgress
        };
    }

    [Fact]
    public async Task ScrapeLastUpdatedDateAsync_ReturnsCorrectDate_WhenDateNodeExists()
    {
        // Arrange
        const string expectedDate = "2025-04-15T00:00:00Z";
        var htmlContent = $@"
            <html>
                <body>
                    <div class='gem-c-metadata'>
                        <time class='gem-c-published-dates__change-date timestamp' datetime='{expectedDate}'>15 April 2025</time>
                    </div>
                </body>
            </html>";

        _handlerSubstitute.SetResponseContent(htmlContent);
        
        var crawler = new Crawler(_configuration, _httpClientFactory);

        // Act
        var result = await crawler.ScrapeLastUpdatedDateAsync(_processLog);

        // Assert
        Assert.Equal(expectedDate, result);
        Assert.Equal(ProcessStatus.InProgress, _processLog.Status);
        Assert.Empty(_processLog.Errors);
    }

    [Fact]
    public async Task ScrapeLastUpdatedDateAsync_ReturnsNull_WhenDateNodeDoesNotExist()
    {
        // Arrange
        var htmlContent = @"
            <html>
                <body>
                    <div>No date node here</div>
                </body>
            </html>";

        _handlerSubstitute.SetResponseContent(htmlContent);
        
        var crawler = new Crawler(_configuration, _httpClientFactory);

        // Act
        var result = await crawler.ScrapeLastUpdatedDateAsync(_processLog);

        // Assert
        Assert.Null(result);
        Assert.Equal(ProcessStatus.Failed, _processLog.Status);
        Assert.Single(_processLog.Errors);
        Assert.Contains("Date node not found", _processLog.Errors.First().Message);
    }

    [Fact]
    public async Task ScrapeAttachmentLinkAsync_ReturnsCorrectUrl_WhenLinkExists()
    {
        // Arrange
        const string expectedUrl = "/government/publications/register-of-licensed-sponsors-workers/download/12345";
        var htmlContent = $@"
            <html>
                <body>
                    <a class='gem-c-attachment__link' href='{expectedUrl}'>Download CSV</a>
                </body>
            </html>";

        _handlerSubstitute.SetResponseContent(htmlContent);
        
        var crawler = new Crawler(_configuration, _httpClientFactory);

        // Act
        var result = await crawler.ScrapeAttachmentLinkAsync(_processLog);

        // Assert
        Assert.Equal(expectedUrl, result);
        Assert.Equal(ProcessStatus.InProgress, _processLog.Status);
        Assert.Empty(_processLog.Errors);
    }

    [Fact]
    public async Task ScrapeAttachmentLinkAsync_ReturnsNull_WhenLinkDoesNotExist()
    {
        // Arrange
        var htmlContent = @"
            <html>
                <body>
                    <a href='/some/other/link'>Not the attachment</a>
                </body>
            </html>";

        _handlerSubstitute.SetResponseContent(htmlContent);
        
        var crawler = new Crawler(_configuration, _httpClientFactory);

        // Act
        var result = await crawler.ScrapeAttachmentLinkAsync(_processLog);

        // Assert
        Assert.Null(result);
        Assert.Equal(ProcessStatus.Failed, _processLog.Status);
        Assert.Single(_processLog.Errors);
        Assert.Contains("Link node not found", _processLog.Errors.First().Message);
    }

    [Fact]
    public async Task ScrapeAttachmentLinkAsync_HandleExceptions_AndUpdatesProcessLog()
    {
        // Arrange
        _handlerSubstitute.SetExceptionResponse(new HttpRequestException("Test exception"));
        
        var crawler = new Crawler(_configuration, _httpClientFactory);

        // Act
        var result = await crawler.ScrapeAttachmentLinkAsync(_processLog);

        // Assert
        Assert.Null(result);
        Assert.Equal(ProcessStatus.Failed, _processLog.Status);
        Assert.Single(_processLog.Errors);
        Assert.Equal("ScrapeAttachmentLinkAsync", _processLog.Errors.First().Method);
    }
}

// Helper class to substitute HttpMessageHandler
public class HttpMessageHandlerSubstitute : HttpMessageHandler
{
    private HttpResponseMessage _response = new(HttpStatusCode.OK);
    private Exception _exception = null;

    public void SetResponseContent(string content)
    {
        _response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content)
        };
        _exception = null;
    }

    public void SetExceptionResponse(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exception != null)
        {
            throw _exception;
        }
        
        return Task.FromResult(_response);
    }
}