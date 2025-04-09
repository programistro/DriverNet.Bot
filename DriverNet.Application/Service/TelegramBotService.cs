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
    private readonly ICycleService _cycleService;
    protected readonly IMcService _mcService;
    private CancellationTokenSource _cts;
    private static Dictionary<long, SurveyState> _surveyStates = new();
    private static DriverState _driverState = new();
    private static AdminStep _adminStep = new();
    private string _tempName = string.Empty;
    private double _percent = 0;
    private string _mcName = string.Empty;
    
    public TelegramBotService(
        string botToken,
        IDriverService driverService,
        IDispatcherService dispatcherService, ICargoService cargoService, IMcService mcService, ICycleService cycleService)
    {
        _botClient = new TelegramBotClient(botToken);
        _driverService = driverService;
        _dispatcherService = dispatcherService;
        _cargoService = cargoService;
        _cycleService = cycleService;
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

        if (message.Chat.Id == -4713702986)
        {
            if (message.Text == "/add-driver")
            {
                _adminStep = AdminStep.AddDriverName;
                await botClient.SendMessage(message.Chat.Id, "Введите имя нового водителя:",
                    cancellationToken: cancellationToken);
                return;
            }
            
            if (message.Text == "/add-dispatcher")
            {
                _adminStep = AdminStep.AddDispatcherName;
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите имя нового диспетчера:",
                    cancellationToken: cancellationToken);
                return;
            }
            else if (message.Text == "/add-mc")
            {
                _adminStep = AdminStep.AddMcName;
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите название новой MC компании:",
                    cancellationToken: cancellationToken);
                return;
            }
            
            switch (_adminStep)
            {
                case AdminStep.AddDispatcherName:
                    _adminStep = AdminStep.AddDispatcherPercent;
                    _tempName = message.Text;
                
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Введите процент диспетчера (например, 1.5 для 1.5%):",
                        cancellationToken: cancellationToken);
                    break;
                case AdminStep.AddDispatcherPercent:
                    if (double.TryParse(message.Text, out double percent))
                    {
                        _percent = percent;
                        _adminStep = AdminStep.AddDispatcherConfirm;
                    
                        var distConfirmKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Да", "confirm_add_dispatcher"),
                            InlineKeyboardButton.WithCallbackData("Нет", "cancel_add_dispatcher")
                        });
                    
                        await botClient.SendMessage(
                            chatId: message.Chat.Id,
                            text: $"Добавить диспетчера?\nИмя: {_tempName}\nПроцент: {_percent}%",
                            replyMarkup: distConfirmKeyboard,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendMessage(
                            chatId: message.Chat.Id,
                            text: "Некорректный формат процента. Введите число (например: 1.5):",
                            cancellationToken: cancellationToken);
                    }
                    break;
                case AdminStep.AddMcName:
                    _adminStep = AdminStep.AddMcConfirm;
                    _tempName = message.Text;
                
                    var mcConfirmKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Да", "confirm_add_mc"),
                        InlineKeyboardButton.WithCallbackData("Нет", "cancel_add_mc")
                    });
                
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"Добавить MC компанию: {message.Text}?",
                        replyMarkup: mcConfirmKeyboard,
                        cancellationToken: cancellationToken);
                    break;
                case AdminStep.AddDriverName:
                    _adminStep = AdminStep.AddDriverMc;
                    _tempName = message.Text;

                    var mcs = await _mcService.GetAllAsync();
                    
                    var buttons = mcs
                        .Select(d => InlineKeyboardButton.WithCallbackData(d.Name, $"drivermc-{d.Name}"))
                        .ToArray();

                    var driverMcKeyboard = new InlineKeyboardMarkup(buttons.Chunk(2));

                    await botClient.SendMessage(message.Chat.Id, $"Выберите MC#:",
                        replyMarkup: driverMcKeyboard, cancellationToken: cancellationToken);
                    break;
                case AdminStep.AddDriverMc:
                    _adminStep = AdminStep.AddMcConfirm;
                    _mcName = message.Text;
                    
                    break;
            }
            
            if (message.Text == "/open-month")
            {
                _cycleService.StartMonth();

                await _botClient.SendMessage(message.Chat.Id, "Месяц для статистики открыт", cancellationToken: cancellationToken);
            }
            else if (message.Text == "/close-month")
            {
                _cycleService.EndMonth();

                await _botClient.SendMessage(message.Chat.Id,
                    $"Месяц для статистики закрыт, статистика велась с {_cycleService.Month} по {_cycleService.LastMonth}",
                    cancellationToken: cancellationToken);
            }
            else if (message.Text == "/stat-month")
            {
                var allCargos = await _cargoService.GetAllAsync();
                var periodCargos = allCargos
                    .Where(c => c.CreatedAt >= _cycleService.Month &&
                                c.CreatedAt <= _cycleService.LastMonth);

                if (!periodCargos.Any())
                {
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text:
                        $"Нет данных за период с {_cycleService.Month} по {(_cycleService.LastMonth)}",
                        cancellationToken: cancellationToken);
                    return;
                }

                // Рассчитываем статистику
                double totalCost = periodCargos.Sum(c => c.CostCargo);
                double totalEmptyMiles = periodCargos.Sum(c => c.WithoutMile);
                double totalLoadedMiles = periodCargos.Sum(c => c.WithMile);
                double totalMiles = totalEmptyMiles + totalLoadedMiles;
                double ratePerMile = totalMiles > 0 ? totalCost / totalMiles : 0;

                // Формируем отчет
                var report = new System.Text.StringBuilder();
                report.AppendLine(
                    $"📊 Статистика за период {_cycleService.Month} - {_cycleService.LastMonth}");
                report.AppendLine();
                report.AppendLine($"📌 Всего грузов: {periodCargos.Count()}");
                report.AppendLine($"💰 Общая сумма: ${totalCost:F2}");
                report.AppendLine($"🛣️ Общий пробег: {totalMiles} миль");
                report.AppendLine($"  ├ Пустых: {totalEmptyMiles} миль");
                report.AppendLine($"  └ Загруженных: {totalLoadedMiles} миль");
                report.AppendLine();
                report.AppendLine($"📈 Rate per mile: ${ratePerMile:F2}");
                report.AppendLine();
                report.AppendLine("Список грузов:");

                foreach (var cargo in periodCargos.OrderBy(c => c.CreatedAt))
                {
                    report.AppendLine(
                        $"  - #{cargo.Number} | {cargo.CreatedAt:dd.MM.yyyy} | ${cargo.CostCargo:F2} | {cargo.WithMile + cargo.WithoutMile} миль");
                }

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: report.ToString(),
                    cancellationToken: cancellationToken);
            }
        }
        
        if (message.Text == "/load")
        {
            StartNewSurvey(message.Chat.Id);
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Введите номер загрузки:",
                cancellationToken: cancellationToken);
            return;
        }

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
        try
        {
            if (callbackQuery.Message.Chat.Id == -4713702986)
            {
                if (callbackQuery.Data.StartsWith("drivermc-"))
                {
                    _mcName = callbackQuery.Data.Replace("drivermc-", "");
                    _adminStep = AdminStep.AddDriverConfirm;
                    
                    var driverConfirmKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Да", "confirm_add_driver"),
                        InlineKeyboardButton.WithCallbackData("Нет", "cancel_add_driver")
                    });

                    await botClient.SendMessage(callbackQuery.Message.Chat.Id, $"Добавить водителя {_tempName} с MC# {_mcName}?",
                        replyMarkup: driverConfirmKeyboard, cancellationToken: cancellationToken);
                    return;
                }
                
                switch (callbackQuery.Data)
                {
                    case "confirm_add_dispatcher":
                        var newDispatcher = new Dispatcher
                        {
                            Id = Guid.NewGuid(),
                            Name = _tempName,
                            Percent = _percent
                        };
                        await _dispatcherService.AddAsync(newDispatcher);

                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: $"Диспетчер {_tempName} успешно добавлен!",
                            cancellationToken: cancellationToken);
                        _adminStep = AdminStep.None;
                        break;

                    case "cancel_add_dispatcher":
                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: "Добавление диспетчера отменено",
                            cancellationToken: cancellationToken);
                        _adminStep = AdminStep.None;
                        break;

                    case "confirm_add_mc":
                        var newMc = new McModel()
                        {
                            Id = Guid.NewGuid(),
                            Name = _tempName 
                        };
                        await _mcService.AddAsync(newMc);

                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: $"MC компания {_tempName} успешно добавлена!",
                            cancellationToken: cancellationToken);
                        _adminStep = AdminStep.None;
                        break;

                    case "cancel_add_mc":
                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: "Добавление MC компании отменено",
                            cancellationToken: cancellationToken);
                        _adminStep = AdminStep.None;
                        break;
                    
                    case "confirm_add_driver":
                        Driver driver = new()
                        {
                            Id = Guid.NewGuid(),
                            Name = _tempName,
                            MCNumber = _mcName
                        };
                        await _driverService.AddAsync(driver);

                        await botClient.SendMessage(callbackQuery.Message.Chat.Id,
                            $"Вводитеьл {_tempName} успешно добавлен!", cancellationToken: cancellationToken);
                        _adminStep = AdminStep.None;
                        break;
                    case "cancel_add_driver":
                        await botClient.SendMessage(
                            chatId: callbackQuery.Message.Chat.Id,
                            text: "Добавление вводителя отменено",
                            cancellationToken: cancellationToken);
                        _adminStep = AdminStep.None;
                        break;
                }
            }

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
                    await botClient.SendMessage(
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
            else if (callbackQuery.Data.StartsWith("change_"))
            {
                await HandleChangeRequest(botClient, callbackQuery, state, chatId, cancellationToken);
            }
            else if (callbackQuery.Data == "cancel_changes")
            {
                await ShowConfirmation(botClient, chatId, state, cancellationToken);
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling callback: {ex.Message}");
            await botClient.SendMessage(
                chatId: callbackQuery.Message.Chat.Id,
                text: "Произошла ошибка. Пожалуйста, попробуйте еще раз.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleChangeRequest(ITelegramBotClient botClient, CallbackQuery callbackQuery, SurveyState state, long chatId, CancellationToken cancellationToken)
    {
        var fieldToChange = callbackQuery.Data.Replace("change_", "");
        
        switch (fieldToChange)
        {
            case "number":
                state.CurrentStep = CargoStep.Number;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Введите новый номер загрузки:",
                    cancellationToken: cancellationToken);
                break;
                
            case "dispatcher":
                state.CurrentStep = CargoStep.Dispatcher;
                await ShowDispatchersKeyboard(botClient, chatId, cancellationToken);
                break;
                
            case "driver":
                state.CurrentStep = CargoStep.Driver;
                await ShowDriversKeyboard(botClient, chatId, cancellationToken);
                break;
                
            case "mc":
                state.CurrentStep = CargoStep.MC;
                await ShowMcCompaniesKeyboard(botClient, chatId, cancellationToken);
                break;
                
            case "mile_empty":
                state.CurrentStep = CargoStep.MileWithoutCargo;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Введите новое количество миль без груза:",
                    cancellationToken: cancellationToken);
                break;
                
            case "mile_loaded":
                state.CurrentStep = CargoStep.MileWithCargo;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Введите новое количество миль с грузом:",
                    cancellationToken: cancellationToken);
                break;
                
            case "cost":
                state.CurrentStep = CargoStep.CostCargo;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Введите новую стоимость груза:",
                    cancellationToken: cancellationToken);
                break;
                
            case "route":
                state.CurrentStep = CargoStep.PathTravel;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Введите новый маршрут:",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleNumberInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        state.Number = message.Text;
        
        if (state.CurrentStep == CargoStep.Number && state.IsEditing)
        {
            await ShowConfirmationAfterChange(botClient, message.Chat.Id, state, cancellationToken);
        }
        else
        {
            state.CurrentStep = CargoStep.Dispatcher;
            await ShowDispatchersKeyboard(botClient, message.Chat.Id, cancellationToken);
        }
    }

    private async Task ShowDispatchersKeyboard(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var dispatchers = await _dispatcherService.GetAllAsync();
        var buttons = dispatchers
            .Select(d => InlineKeyboardButton.WithCallbackData(d.Name, $"dispatcher_{d.Name}"))
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons.Chunk(2));
        
        await botClient.SendMessage(
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
        
        await botClient.SendMessage(
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
        
        await botClient.SendMessage(
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
            
            if (state.IsEditing)
            {
                await ShowConfirmationAfterChange(botClient, message.Chat.Id, state, cancellationToken);
            }
            else
            {
                state.CurrentStep = CargoStep.MileWithCargo;
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите сколько миль с грузом:",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(
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
            
            if (state.IsEditing)
            {
                await ShowConfirmationAfterChange(botClient, message.Chat.Id, state, cancellationToken);
            }
            else
            {
                state.CurrentStep = CargoStep.CostCargo;
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите сколько платят за груз:",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(
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
            
            if (state.IsEditing)
            {
                await ShowConfirmationAfterChange(botClient, message.Chat.Id, state, cancellationToken);
            }
            else
            {
                state.CurrentStep = CargoStep.PathTravel;
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите маршрут (например: Москва - Санкт-Петербург):",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Пожалуйста, введите корректную сумму:",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandlePathTravelInputAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        state.PathTravel = message.Text;
        
        if (state.IsEditing)
        {
            await ShowConfirmationAfterChange(botClient, message.Chat.Id, state, cancellationToken);
        }
        else
        {
            state.CurrentStep = CargoStep.Confirmation;
            await ShowConfirmation(botClient, message.Chat.Id, state, cancellationToken);
        }
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
            new[] { InlineKeyboardButton.WithCallbackData("Отменить изменения", "cancel_changes") },
        });
        
        await botClient.SendMessage(
            chatId: chatId,
            text: "Что вы хотите изменить?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task ShowConfirmation(ITelegramBotClient botClient, long chatId, SurveyState state, CancellationToken cancellationToken)
    {
        state.CurrentStep = CargoStep.Confirmation;
        state.IsEditing = false;
        
        var findDriver = await _driverService.GetByIdAsync(Guid.Parse(state.DriverId));
        var findDispatcher = await _dispatcherService.GetByIdAsync(Guid.Parse(state.DispatcherId));
        var findMc = await _mcService.GetByIdAsync(Guid.Parse(state.McId));
        
        var summary = $"Проверьте данные:\n\n" +
                     $"Номер: {state.Number}\n" +
                     $"Диспетчер: {findDriver.Name}\n" +
                     $"Водитель: {findDispatcher.Name}\n" +
                     $"MC: {findMc.Name}\n" +
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
        
        await botClient.SendMessage(
            chatId: chatId,
            text: summary,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task ShowConfirmationAfterChange(ITelegramBotClient botClient, long chatId, SurveyState state, CancellationToken cancellationToken)
    {
        state.IsEditing = true;
        await botClient.SendMessage(
            chatId: chatId,
            text: "Изменения сохранены!",
            cancellationToken: cancellationToken);
            
        await ShowConfirmation(botClient, chatId, state, cancellationToken);
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
        
        await botClient.SendMessage(
            chatId: chatId,
            text: "Груз успешно сохранен!",
            cancellationToken: cancellationToken);
    }

    private void StartNewSurvey(long chatId)
    {
        _surveyStates[chatId] = new SurveyState
        {
            CurrentStep = CargoStep.Number,
            IsEditing = false
        };
    }

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