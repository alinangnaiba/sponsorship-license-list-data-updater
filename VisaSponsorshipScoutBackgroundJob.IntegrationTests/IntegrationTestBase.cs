using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using Testcontainers.RavenDb;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;
using VisaSponsorshipScoutBackgroundJob.Services;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Core;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client.Documents.Session;

namespace VisaSponsorshipScoutBackgroundJob.IntegrationTests;

/// <summary>
/// Base class for integration tests that sets up the test environment including RavenDB container
/// </summary>
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly RavenDbContainer _ravenDbContainer;
    protected IHost _host = null!;
    protected ServiceCollection _baseServices = null!;
    
    protected IntegrationTestBase()
    {
        _ravenDbContainer = new RavenDbBuilder()
            .WithImage("ravendb/ravendb:latest")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();
    }

    public virtual async Task InitializeAsync()
    {
        // Start the container
        await _ravenDbContainer.StartAsync();
        
        // Configure services
        _baseServices = new ServiceCollection();
        
        // Setup configuration
        var configDict = new Dictionary<string, string>
        {
            ["ApplicationSettings:FileStoragePath"] = "test-storage",
            ["ApplicationSettings:CsvUrl"] = "https://test.gov.uk/sponsors"
        };
        
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(configDict);
        
        var configuration = configBuilder.Build();
        
        // Setup RavenDB document store directly instead of using ConfigureDatabase extension
        var store = new DocumentStore
        {
            Urls = new[] { _ravenDbContainer.GetConnectionString() },
            Database = "TestDb"
        };
        store.Initialize();
        
        // Create database if it doesn't exist
        try
        {
            var dbRecord = store.Maintenance.Server.Send(new GetDatabaseRecordOperation("TestDb"));
            if (dbRecord == null)
            {
                store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord("TestDb")));
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize database: {ex.Message}", ex);
        }
        
        // Add index definitions
        new Organisation_ByName().Execute(store);
        new ProcessLog_ByName().Execute(store);
        
        // Register the document store with DI
        _baseServices.AddSingleton<IDocumentStore>(store);
        
        // Register all services we need for testing
        _baseServices.AddHttpClient();
        _baseServices.AddScoped<IFileProcessor, FileProcessor>();
        
        // Register test implementations for external dependencies
        _baseServices.AddTransient<ICrawler, TestCrawler>();
        _baseServices.AddTransient<IFileDownloadClient, TestFileDownloadClient>();
        _baseServices.AddTransient<IFileStorageService, TestFileStorageService>();
        _baseServices.AddTransient<IOrganisationFileService, OrganisationFileService>();
        
        // Build the host with these services
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddConfiguration(configuration);
            })
            .ConfigureServices(services =>
            {
                // Add all the services we've configured
                foreach (var descriptor in _baseServices)
                {
                    // Explicitly cast the descriptor to avoid type inference issues
                    services.Add((ServiceDescriptor)descriptor);
                }
            });
        
        _host = hostBuilder.Build();
    }

    public virtual async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _ravenDbContainer.StopAsync();
        await _ravenDbContainer.DisposeAsync();
    }
    
    /// <summary>
    /// Helper method to create a new ServiceCollection with all the current services except the ones to replace
    /// </summary>
    protected virtual ServiceCollection CreateServiceCollectionWithReplacements(params Type[] typesToReplace)
    {
        var serviceCollection = new ServiceCollection();
        
        foreach (var descriptor in _baseServices)
        {
            if (!typesToReplace.Contains(descriptor.ServiceType))
            {
                // Explicitly cast the descriptor to avoid type inference issues
                serviceCollection.Add((ServiceDescriptor)descriptor);
            }
        }
        
        return serviceCollection;
    }
}

// Test implementations of services that interact with external systems

/// <summary>
/// Test implementation of the Crawler that returns predefined responses
/// </summary>
public class TestCrawler : ICrawler
{
    private readonly string _lastUpdatedDate;
    private readonly string _attachmentLink;
    private readonly bool _shouldFail;

    public TestCrawler()
    {
        _lastUpdatedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        _attachmentLink = "https://test.gov.uk/download/test-sponsors.csv";
        _shouldFail = false;
    }

    public TestCrawler(string lastUpdatedDate, string attachmentLink, bool shouldFail = false)
    {
        _lastUpdatedDate = lastUpdatedDate;
        _attachmentLink = attachmentLink;
        _shouldFail = shouldFail;
    }

    public async Task<string?> ScrapeAttachmentLinkAsync(ProcessLog processLog)
    {
        await Task.Delay(100); // Simulate network delay
        
        if (_shouldFail)
        {
            processLog.Status = ProcessStatus.Failed;
            processLog.Errors.Add(new ProcessError { Message = "Test failure in ScrapeAttachmentLinkAsync" });
            return null;
        }
        
        return _attachmentLink;
    }

    public async Task<string?> ScrapeLastUpdatedDateAsync(ProcessLog processLog)
    {
        await Task.Delay(100); // Simulate network delay
        
        if (_shouldFail)
        {
            processLog.Status = ProcessStatus.Failed;
            processLog.Errors.Add(new ProcessError { Message = "Test failure in ScrapeLastUpdatedDateAsync" });
            return null;
        }
        
        return _lastUpdatedDate;
    }
}

/// <summary>
/// Test implementation of the FileDownloadClient that returns predefined content
/// </summary>
public class TestFileDownloadClient : IFileDownloadClient
{
    private readonly byte[] _fileContent;
    private readonly bool _shouldFail;

    public TestFileDownloadClient()
    {
        _fileContent = CreateTestCsvContent();
        _shouldFail = false;
    }

    public TestFileDownloadClient(byte[] fileContent, bool shouldFail = false)
    {
        _fileContent = fileContent;
        _shouldFail = shouldFail;
    }

    public async Task<byte[]?> DownloadFileAsByteArrayAsync(string fileUrl)
    {
        await Task.Delay(100); // Simulate network delay
        
        if (_shouldFail)
        {
            throw new HttpRequestException("Test download failure", null, HttpStatusCode.InternalServerError);
        }
        
        return _fileContent;
    }
    
    private static byte[] CreateTestCsvContent()
    {
        string csvContent = @"Organisation Name,Town/City,County,Type & Rating,Route
Acme Corporation,London,Greater London,A (Premium),Skilled Worker
Acme Corporation,Manchester,Greater Manchester,A (Premium),Skilled Worker
Beta Industries,Birmingham,West Midlands,B (Standard),Skilled Worker
Gamma Ltd,Cardiff,South Glamorgan,A (Premium),Scale-up";
        
        return System.Text.Encoding.UTF8.GetBytes(csvContent);
    }
}

/// <summary>
/// Test implementation of the FileStorageService that simulates cloud storage
/// </summary>
public class TestFileStorageService : IFileStorageService
{
    private readonly Dictionary<string, byte[]> _storage = new();
    private readonly bool _shouldFail;

    public TestFileStorageService(bool shouldFail = false)
    {
        _shouldFail = shouldFail;
    }

    public async Task<string> UploadFileAsync(byte[] fileContent, string fileName)
    {
        await Task.Delay(100); // Simulate network delay
        
        if (_shouldFail)
        {
            throw new Exception("Test storage failure");
        }
        
        var fileKey = $"{Guid.NewGuid()}-{fileName}";
        _storage[fileKey] = fileContent;
        
        return fileKey;
    }

    public async Task<byte[]?> DownloadFileAsync(string fileKey)
    {
        await Task.Delay(100); // Simulate network delay
        
        if (_shouldFail)
        {
            throw new Exception("Test download failure");
        }
        
        return _storage.TryGetValue(fileKey, out var content) ? content : null;
    }
}