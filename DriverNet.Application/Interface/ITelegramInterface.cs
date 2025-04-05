namespace DriverNet.Application.Interface;

public interface ITelegramBotService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}