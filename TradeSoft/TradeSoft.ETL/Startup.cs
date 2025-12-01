using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeSoft.Core.Interfaces;
using TradeSoft.Infrastructure.Dukascopy;
using TradeSoft.Infrastructure.Persistence;

namespace TradeSoft.ETL
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        {
            // 1. On charge la configuration ici
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();
        }

        // 2. On configure l'Injection de Dépendances
        public void ConfigureServices(IServiceCollection services)
        {
            // On rend la configuration accessible partout
            services.AddSingleton(Configuration);

            // --- Enregistrement de l'Infrastructure ---

            // A. Base de données
            // On lit la chaîne de connexion et on prépare le Repository
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            services.AddTransient<IMarketRepository>(provider => new SqlMarketRepository(connectionString));

            // B. Téléchargement
            // On lit l'URL et on prépare le Downloader
            string dukascopyUrl = Configuration["DataSources:DukascopyBaseUrl"];
            services.AddTransient<IDataDownloader>(provider => new DukascopyDownloader(dukascopyUrl));
        }
    }
}