using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;
using VisaSponsorshipScoutBackgroundJob.Services;

namespace VisaSponsorshipScoutBackgroundJob
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    configuration.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    configuration.AddEnvironmentVariables();
                })
                .ConfigureServices((hostingContext, services) =>
                {
                    services.AddHttpClient();
                    services.AddScoped<ICrawler, Crawler>();
                    services.AddScoped<IFileDownloadClient, OrganisationFileDownloadClient>();
                    services.AddScoped<IFileStorageService, GoogleCloudStorageService>();
                    services.AddScoped<IOrganisationFileService, OrganisationFileService>();

                    services.ConfigureDatabase(hostingContext.Configuration);

                    services.AddScoped<IFileProcessor, FileProcessor>();
                })
                .Build();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                Console.WriteLine("Processing started...");
                var processor = host.Services.GetRequiredService<IFileProcessor>();
                await processor.ProcessAsync();
            }
            catch (Exception ex)
            {
                //Initialization error goes here
                Console.Error.WriteLine($"Error during processing: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                Console.WriteLine($"Processing completed in {stopwatch.Elapsed}");
                await host.StopAsync();
            }
        }
    }
}