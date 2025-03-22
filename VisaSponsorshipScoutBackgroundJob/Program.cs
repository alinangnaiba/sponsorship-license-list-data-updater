using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VisaSponsorshipScoutBackgroundJob.Infrastructure.Http;
using static Raven.Client.Constants;

namespace VisaSponsorshipScoutBackgroundJob
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Set base path
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // Load appsettings.json
            .AddEnvironmentVariables() // Load environment variables
            .Build();
            var services = new ServiceCollection();
            services.AddHttpClient(); //
            var serviceProvider = services.BuildServiceProvider();
            IHttpClientFactory clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var webScraper = new WebScraper(clientFactory);
            Console.WriteLine("Hello, World!");
        }
    }
}
