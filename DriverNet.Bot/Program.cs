using DriverNet.Application.Interface;
using DriverNet.Application.Service;
using DriverNet.Core.Interface;
using DriverNet.Infrastructure.Data;
using DriverNet.Infrastructure.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DriverNet.Bot;

class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var botService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
        
        try
        {
            // await botService.StartAsync(CancellationToken.None);
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application error: {ex.Message}");
        }
        finally
        {
            await botService.StopAsync(CancellationToken.None);
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .Build();

        string botToken = "7769384625:AAGvwQjURncRstEcILTjXXpPhGaiMEDevQ4";
        
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, services) =>
            {
                services.AddConfiguration(configuration);
                
                // botToken = context.Configuration.GetConnectionString("TelegramBot:Token");
            })
            .ConfigureServices((builder =>
            {
                builder.AddDbContextFactory<AppDbContext>();
                builder.AddSingleton<IDriverRepository, DriverRepository>();
                builder.AddSingleton<IDispatcherRepository, DispatcherRepository>();
                builder.AddSingleton<ICargoRepository, CargoRepository>();
                builder.AddSingleton<IDriverService, DriverService>();
                builder.AddSingleton<IDispatcherService, DispatcherService>();
                builder.AddSingleton<ICargoService, CargoService>();
                builder.AddSingleton<IMcRepository, McRepository>();
                builder.AddSingleton<IMcService, McService>();
                builder.AddSingleton<ICycleService, CycleService>();
                builder.AddSingleton<ITelegramBotService>(provider =>
                    new TelegramBotService(
                        botToken,
                        provider.GetRequiredService<IDriverService>(),
                        provider.GetRequiredService<IDispatcherService>(),
                        provider.GetRequiredService<ICargoService>(),
                        provider.GetRequiredService<IMcService>(),
                        provider.GetRequiredService<ICycleService>()));
            
                builder.AddHostedService<BotBackgroundService>();
            }));
    }
    
}