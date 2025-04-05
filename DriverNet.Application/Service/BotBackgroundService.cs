using DriverNet.Application.Interface;
using Microsoft.Extensions.Hosting;

namespace DriverNet.Application.Service;

public class BotBackgroundService : BackgroundService
{
    private readonly ITelegramBotService _botService;

    public BotBackgroundService(ITelegramBotService botService)
    {
        _botService = botService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _botService.StartAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _botService.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}