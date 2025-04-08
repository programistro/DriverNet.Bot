using System.Xml.Linq;
using DriverNet.Application.Interface;
using DriverNet.Core.Interface;
using DriverNet.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DriverNet.Application.Service;

public class TelegramBotService : ITelegramBotService, IDisposable
{
    private readonly TelegramBotClient _botClient;
    private readonly IDriverService _driverService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ICargoService _cargoService;
    protected readonly IMcService _mcService;
    private CancellationTokenSource _cts;
    private static Dictionary<long, SurveyState> _surveyStates = new();
    private static DriverState _driverState = new();
    private static AdminStep _adminStep = new();

    public TelegramBotService(
        string botToken,
        IDriverService driverService,
        IDispatcherService dispatcherService, ICargoService cargoService, IMcService mcService)
    {
        _botClient = new TelegramBotClient(botToken);
        _driverService = driverService;
        _dispatcherService = dispatcherService;
        _cargoService = cargoService;
        _mcService = mcService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var me = await _botClient.GetMeAsync(cancellationToken);
        Console.WriteLine($"Bot {me.Username} started");
        
        try
        {
            var webhookInfo = await _botClient.GetWebhookInfoAsync(cancellationToken);
            if (!string.IsNullOrEmpty(webhookInfo.Url))
            {
                Console.WriteLine($"Deleting active webhook: {webhookInfo.Url}");
                await _botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking/deleting webhook: {ex.Message}");
        }
        
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            },
            cancellationToken: _cts.Token
        );
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        Console.WriteLine("Bot stopped");
    }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery != null)
        {
            await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
            return;
        }

        var message = update.Message;
        if (message == null) return;

        // Обработка команды /load
        if (message.Text == "/load")
        {
            StartNewSurvey(message.Chat.Id);
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите номер загрузки:",
                cancellationToken: cancellationToken);
            return;
        }

        // Обработка шагов опроса
        if (_surveyStates.TryGetValue(message.Chat.Id, out var currentState))
        {
            switch (currentState.CurrentStep)
            {
                case CargoStep.Number:
                    await HandleNumberInputAsync(botClient, message, currentState, cancellationToken);
                    break;
                    
                case CargoStep.MileWithCargo:
                    await HandleMileWithCargoInputAsync(botClient, message, currentState, cancellationToken);
                    break;
                    
                case CargoStep.MileWithoutCargo:
                    await HandleMileWithoutCargoInputAsync(botClient, message, currentState, cancellationToken);
                    break;
                    
                case CargoStep.CostCargo:
                    await HandleCostCargoInputAsync(botClient, message, currentState, cancellationToken);
                    break;
                    
                case CargoStep.PathTravel:
                    await HandlePathTravelInputAsync(botClient, message, currentState, cancellationToken);
                    break;
            }
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        if (!_surveyStates.TryGetValue(chatId, out var state)) return;

        if (callbackQuery.Data.StartsWith("dispatcher_"))
        {
            var dispatcherName = callbackQuery.Data.Replace("dispatcher_", "");
            var dispatcher = await _dispatcherService.GetByNameAsync(dispatcherName);
            
            if (dispatcher != null)
            {
                state.DispatcherId = dispatcher.Id.ToString();
                state.CurrentStep = CargoStep.Driver;
                await ShowDriversKeyboard(botClient, chatId, cancellationToken);
            }
        }
        else if (callbackQuery.Data.StartsWith("driver_"))
        {
            var driverName = callbackQuery.Data.Replace("driver_", "");
            var driver = await _driverService.GetByNameAsync(driverName);
            
            if (driver != null)
            {
                state.DriverId = driver.Id.ToString();
                state.CurrentStep = CargoStep.MC;
                await ShowMcCompaniesKeyboard(botClient, chatId, cancellationToken);
            }
        }
        else if (callbackQuery.Data.StartsWith("mc_"))
        {
            var mcName = callbackQuery.Data.Replace("mc_", "");
            var mc = await _mcService.GetByNameAsync(mcName);
            
            if (mc != null)
            {
                state.McId = mc.Id.ToString();
                state.CurrentStep = CargoStep.MileWithoutCargo;
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Введите сколько миль пустым:",
                    cancellationToken: cancellationToken);
            }
        }
        else if (callbackQuery.Data == "confirm_yes")
        {
            await SaveCargoAndFinish(botClient, chatId, state, cancellationToken);
        }
        else if (callbackQuery.Data == "confirm_no")
        {
            state.CurrentStep = CargoStep.ChangeField;
            await AskWhatToChange(botClient, chatId, cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleNumberInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        state.Number = message.Text;
        state.CurrentStep = CargoStep.Dispatcher;
        await ShowDispatchersKeyboard(botClient, message.Chat.Id, cancellationToken);
    }

    private async Task ShowDispatchersKeyboard(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var dispatchers = await _dispatcherService.GetAllAsync();
        var buttons = dispatchers
            .Select(d => InlineKeyboardButton.WithCallbackData(d.Name, $"dispatcher_{d.Name}"))
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons.Chunk(2));
        
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите диспетчера:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task ShowDriversKeyboard(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var drivers = await _driverService.GetAllAsync();
        var buttons = drivers
            .Select(d => InlineKeyboardButton.WithCallbackData(d.Name, $"driver_{d.Name}"))
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons.Chunk(2));
        
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите водителя:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task ShowMcCompaniesKeyboard(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var mcs = await _mcService.GetAllAsync();
        var buttons = mcs
            .Select(m => InlineKeyboardButton.WithCallbackData(m.Name, $"mc_{m.Name}"))
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons.Chunk(2));
        
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите MC компанию:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleMileWithoutCargoInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        if (double.TryParse(message.Text, out var miles))
        {
            state.MileWithoutCargo = miles;
            state.CurrentStep = CargoStep.MileWithCargo;
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите сколько миль с грузом:",
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Пожалуйста, введите корректное число:",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleMileWithCargoInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        if (double.TryParse(message.Text, out var miles))
        {
            state.MileWithCargo = miles;
            state.CurrentStep = CargoStep.CostCargo;
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите сколько платят за груз:",
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Пожалуйста, введите корректное число:",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleCostCargoInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        if (double.TryParse(message.Text, out var cost))
        {
            state.CostCargo = cost;
            state.CurrentStep = CargoStep.PathTravel;
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите маршрут:",
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Пожалуйста, введите корректную сумму:",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandlePathTravelInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        state.PathTravel = message.Text;
        state.CurrentStep = CargoStep.Confirmation;
        
        var summary = $"Проверьте данные:\n\n" +
                     $"Номер: {state.Number}\n" +
                     $"Диспетчер: {state.DispatcherId}\n" +
                     $"Водитель: {state.DriverId}\n" +
                     $"MC: {state.McId}\n" +
                     $"Миль без груза: {state.MileWithoutCargo}\n" +
                     $"Миль с грузом: {state.MileWithCargo}\n" +
                     $"Оплата: {state.CostCargo}\n" +
                     $"Маршрут: {state.PathTravel}\n\n" +
                     $"Все верно?";
        
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("Да", "confirm_yes"),
            InlineKeyboardButton.WithCallbackData("Нет", "confirm_no"),
        });
        
        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: summary,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task AskWhatToChange(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Номер", "change_number") },
            new[] { InlineKeyboardButton.WithCallbackData("Диспетчера", "change_dispatcher") },
            new[] { InlineKeyboardButton.WithCallbackData("Водителя", "change_driver") },
            new[] { InlineKeyboardButton.WithCallbackData("MC компанию", "change_mc") },
            new[] { InlineKeyboardButton.WithCallbackData("Миль без груза", "change_mile_empty") },
            new[] { InlineKeyboardButton.WithCallbackData("Миль с грузом", "change_mile_loaded") },
            new[] { InlineKeyboardButton.WithCallbackData("Оплату", "change_cost") },
            new[] { InlineKeyboardButton.WithCallbackData("Маршрут", "change_route") },
        });
        
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Что вы хотите изменить?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SaveCargoAndFinish(ITelegramBotClient botClient, long chatId, SurveyState state, CancellationToken cancellationToken)
    {
        var cargo = new Cargo
        {
            Id = Guid.NewGuid(),
            Number = state.Number,
            DispatcherId = state.DispatcherId,
            MC = state.McId,
            WithoutMile = state.MileWithoutCargo,
            WithMile = state.MileWithCargo,
            CostCargo = state.CostCargo,
            PathTravel = state.PathTravel,
        };
        
        await _cargoService.AddAsync(cargo);
        _surveyStates.Remove(chatId);
        
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Груз успешно сохранен!",
            cancellationToken: cancellationToken);
    }

    private void StartNewSurvey(long chatId)
    {
        _surveyStates[chatId] = new SurveyState
        {
            CurrentStep = CargoStep.Number
        };
    }

    // ... (остальные методы остаются без изменений)
    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Polling error: {exception.Message}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}

public class SurveyState
{
    public CargoStep CurrentStep { get; set; }
    public string Number { get; set; }
    public string DispatcherId { get; set; }
    public string DriverId { get; set; }
    public string McId { get; set; }
    public double MileWithoutCargo { get; set; }
    public double MileWithCargo { get; set; }
    public double CostCargo { get; set; }
    public string PathTravel { get; set; }
}

public enum CargoStep
{
    None,
    Number,
    Dispatcher,
    Driver,
    MC,
    MileWithoutCargo,
    MileWithCargo,
    CostCargo,
    PathTravel,
    Confirmation,
    ChangeField
}
