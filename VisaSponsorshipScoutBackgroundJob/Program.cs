using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.CloudServices;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;
using VisaSponsorshipScoutBackgroundJob.Services;

namespace VisaSponsorshipScoutBackgroundJob
{
    internal class Program
    {
        //static void Main(string[] args)
        //{
        //    IConfiguration configuration = new ConfigurationBuilder()
        //    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Set base path
        //    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // Load appsettings.json
        //    .AddEnvironmentVariables() // Load environment variables
        //    .Build();
        //    var services = new ServiceCollection();
        //    services.AddHttpClient(); //
        //    services.AddSingleton(configuration);
        //    services.AddScoped<ICrawler, Crawler>();
        //    services.AddScoped<IFileDownloadClient, OrganisationFileDownloadClient>();
        //    services.AddScoped<IFileStorageService, GoogleCloudStorageService>();
        //    services.AddScoped<IFileService, FileService>();
        //    services.AddScoped<IFileProcessor, FileProcessor>();
        //    services.ConfigureDatabase(configuration);

        //    var serviceProvider = services.BuildServiceProvider();

        //    var processor = serviceProvider.GetRequiredService<IFileProcessor>();
        //    processor.ProcessAsync().Wait();
        //}
        static async Task Main(string[] args)
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

            try
            {
                Console.WriteLine("Processing started...");
                var processor = host.Services.GetRequiredService<IFileProcessor>();
                await processor.ProcessAsync();
                Console.WriteLine("Processing completed...");
            }
            catch (Exception ex)
            {
                //Initialization error goes here
                Console.Error.WriteLine($"Error during processing: {ex.Message}");
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}
