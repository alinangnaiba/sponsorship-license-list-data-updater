using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Raven.Client.Documents;
using VisaSponsorshipScoutBackgroundJob.Core;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;
using VisaSponsorshipScoutBackgroundJob.Services;

namespace VisaSponsorshipScoutBackgroundJob.IntegrationTests;

/// <summary>
/// Integration tests for the FileProcessor with a real RavenDB instance
/// </summary>
[Collection("IntegrationTests")]
public class FileProcessorIntegrationTests : IntegrationTestBase, IClassFixture<RavenDbTestFixture>
{
    [Fact]
    public async Task ProcessAsync_CompletesSuccessfully_WithNewData()
    {
        // Arrange
        // Get service provider from host
        var serviceProvider = _host.Services;
        
        // Get a document store to check the results
        var documentStore = serviceProvider.GetRequiredService<IDocumentStore>();
        
        // Get the file processor
        var fileProcessor = serviceProvider.GetRequiredService<IFileProcessor>();
        
        // Act
        await fileProcessor.ProcessAsync();
        
        // Assert
        using var session = documentStore.OpenSession();
        
        // Verify that a process log was created
        var processLog = session.Query<ProcessLog>()
            .FirstOrDefault();
            
        Assert.NotNull(processLog);
        Assert.Equal(ProcessStatus.Completed, processLog.Status);
        Assert.NotNull(processLog.FinishedAt);
        Assert.True(processLog.AddedRecords.Count > 0);
        
        // Verify organizations were stored
        var organisations = session.Query<Organisation>().ToList();
        Assert.NotEmpty(organisations);
        
        // Verify correct parsing and merging of duplicate organization names
        var acme = organisations.FirstOrDefault(o => o.Name == "Acme Corporation");
        Assert.NotNull(acme);
        Assert.Equal(2, acme.TownCities.Count); // London and Manchester
        Assert.Contains("London", acme.TownCities);
        Assert.Contains("Manchester", acme.TownCities);
    }
    
    [Fact]
    public async Task ProcessAsync_ReportsNoUpdate_WhenNoNewData()
    {
        // Arrange
        // First run to populate the database
        await ProcessAsync_CompletesSuccessfully_WithNewData();
        
        // Create a new custom TestCrawler with an older date than our first run
        var oldDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var testCrawler = new TestCrawler(oldDate, "https://test.gov.uk/download/old-file.csv");
        
        // Replace the crawler in DI using our helper method
        var serviceCollection = CreateServiceCollectionWithReplacements(typeof(ICrawler));
        serviceCollection.AddSingleton<ICrawler>(testCrawler);
        
        // Build a new service provider with our custom crawler
        var customServiceProvider = serviceCollection.BuildServiceProvider();
        
        // Get the file processor with our custom service provider
        var fileProcessor = customServiceProvider.GetRequiredService<IFileProcessor>();
        
        // Act
        await fileProcessor.ProcessAsync();
        
        // Assert
        using var session = customServiceProvider.GetRequiredService<IDocumentStore>().OpenSession();
        
        // Get the most recent process log
        var processLog = session.Query<ProcessLog>()
            .OrderByDescending(log => log.StartedAt)
            .FirstOrDefault();
            
        Assert.NotNull(processLog);
        Assert.Equal(ProcessStatus.NoUpdate, processLog.Status);
    }

    [Fact]
    public async Task ProcessAsync_TracksChanges_WhenOrganizationsChangeOrAreRemoved()
    {
        // Arrange
        // First run to populate the database
        await ProcessAsync_CompletesSuccessfully_WithNewData();
        
        // Create modified test data - remove one organization and change another
        string modifiedCsv = @"Organisation Name,Town/City,County,Type & Rating,Route
Acme Corporation,London,Surrey,A (Premium),Skilled Worker
Beta Industries,Birmingham,West Midlands,A (Premium),Global Business Mobility";
        
        byte[] modifiedContent = System.Text.Encoding.UTF8.GetBytes(modifiedCsv);
        
        // Create a new TestFileDownloadClient with modified data
        var testFileDownloadClient = new TestFileDownloadClient(modifiedContent);
        
        // Create a new TestCrawler with newer date
        var newDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var testCrawler = new TestCrawler(newDate, "https://test.gov.uk/download/updated-file.csv");
        
        // Replace the services in DI using our helper method
        var serviceCollection = CreateServiceCollectionWithReplacements(typeof(ICrawler), typeof(IFileDownloadClient));
        serviceCollection.AddSingleton<ICrawler>(testCrawler);
        serviceCollection.AddSingleton<IFileDownloadClient>(testFileDownloadClient);
        
        // Build a new service provider with our custom services
        var customServiceProvider = serviceCollection.BuildServiceProvider();
        
        // Get the file processor with our custom service provider
        var fileProcessor = customServiceProvider.GetRequiredService<IFileProcessor>();
        
        // Act
        await fileProcessor.ProcessAsync();
        
        // Assert
        using var session = customServiceProvider.GetRequiredService<IDocumentStore>().OpenSession();
        
        // Get the most recent process log
        var processLog = session.Query<ProcessLog>()
            .OrderByDescending(log => log.StartedAt)
            .FirstOrDefault();
            
        Assert.NotNull(processLog);
        Assert.Equal(ProcessStatus.Completed, processLog.Status);
        
        // Verify deletions
        Assert.Equal(1, processLog.DeletedRecords.Count);
        Assert.Contains("Gamma Ltd", processLog.DeletedRecords.OrganisationNames);
        
        // Verify updates
        Assert.True(processLog.UpdatedRecords.Count > 0);
        var updates = processLog.UpdatedRecords.UpdateRecordDetails;
        Assert.Contains(updates, u => u.Current.Name == "Acme Corporation" && u.New.County == "Surrey");
        
        // Verify Beta Industries rating & route changed (A (Premium) and Global Business Mobility)
        Assert.Contains(updates, u => u.Current.Name == "Beta Industries" && 
                              u.Current.TypeAndRating.Contains("B (Standard)") && 
                              u.New.TypeAndRating.Contains("A (Premium)"));
        
        // Check actual database state
        var organisations = session.Query<Organisation>().ToList();
        
        // Only 2 should remain
        Assert.Equal(2, organisations.Count);
        
        // No Gamma Ltd
        Assert.DoesNotContain(organisations, o => o.Name == "Gamma Ltd");
        
        // Acme should have updated county
        var acme = organisations.FirstOrDefault(o => o.Name == "Acme Corporation");
        Assert.NotNull(acme);
        Assert.Equal("Surrey", acme.County);
        
        // Beta should have updated rating and route
        var beta = organisations.FirstOrDefault(o => o.Name == "Beta Industries");
        Assert.NotNull(beta);
        Assert.Contains("A (Premium)", beta.TypeAndRatings);
        Assert.Contains("Global Business Mobility", beta.Routes);
    }
    
    [Fact]
    public async Task ProcessAsync_HandlesErrors_FromExternalDependencies()
    {
        // Arrange
        // Create test implementations that fail
        var failingCrawler = new TestCrawler(
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            "https://test.gov.uk/download/file.csv", 
            shouldFail: true);
            
        // Replace the crawler in DI using our helper method
        var serviceCollection = CreateServiceCollectionWithReplacements(typeof(ICrawler));
        serviceCollection.AddSingleton<ICrawler>(failingCrawler);
        
        // Build a new service provider with our failing crawler
        var customServiceProvider = serviceCollection.BuildServiceProvider();
        
        // Get the file processor with our custom service provider
        var fileProcessor = customServiceProvider.GetRequiredService<IFileProcessor>();
        
        // Act
        await fileProcessor.ProcessAsync();
        
        // Assert
        using var session = customServiceProvider.GetRequiredService<IDocumentStore>().OpenSession();
        
        // Get the process log
        var processLog = session.Query<ProcessLog>()
            .FirstOrDefault();
            
        Assert.NotNull(processLog);
        Assert.Equal(ProcessStatus.Failed, processLog.Status);
        Assert.NotEmpty(processLog.Errors);
        Assert.Contains(processLog.Errors, e => e.Message.Contains("Test failure"));
    }
}

/// <summary>
/// Required for xUnit so that containers aren't created/destroyed for each test
/// </summary>
public class RavenDbTestFixture : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}