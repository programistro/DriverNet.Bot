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
    private CancellationTokenSource _cts;
    private static Dictionary<long, SurveyState> _surveyStates = new();
    private static DriverState _driverState = new();

    public TelegramBotService(
        string botToken,
        IDriverService driverService,
        IDispatcherService dispatcherService, ICargoService cargoService)
    {
        _botClient = new TelegramBotClient(botToken);
        _driverService = driverService;
        _dispatcherService = dispatcherService;
        _cargoService = cargoService;
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

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if(update == null)
            return;
        
        if (update.CallbackQuery != null)
        {
            if (update.CallbackQuery.Data.StartsWith("dispatcher-"))
            {
                var findDispatcher = await _dispatcherService.GetByNameAsync(update.CallbackQuery.Data.Replace("dispatcher-", ""));
                
                _surveyStates[update.CallbackQuery.Message.Chat.Id].CurrentStep = CargoStep.Dispatcher;
                _surveyStates[update.CallbackQuery.Message.Chat.Id].Dispatcher = findDispatcher.Id.ToString();

                await HandleDispatcherAsync(_botClient, update.CallbackQuery.Message,
                    _surveyStates[update.CallbackQuery.Message.Chat.Id], cancellationToken);
                
                return;
            }

            if (update.CallbackQuery.Data.StartsWith("change-"))
            {
                switch (update.CallbackQuery.Data.Replace("number-", ""))
                {
                    case "number":
                        await HandleNumberAsync(_botClient, update.CallbackQuery.Message,
                            _surveyStates[update.CallbackQuery.Message.Chat.Id],
                            cancellationToken);
                        break;
                }
                
                return;
            }
        }
        
        var message = update.Message;
        
        if (!string.IsNullOrEmpty(message?.Text) && message.Chat.Id == -4713702986)
        {
            if (message.Text == "/add-driver")
            {
                _driverState.Step = DriverStep.Name;

                switch (_driverState.Step)
                {
                    case DriverStep.Name:
                        await HandleNameDriver(botClient, message, _driverState, cancellationToken);
                        break;
                    case DriverStep.McNumber:
                        await HandleMCNumber(botClient, message, _driverState, cancellationToken);
                        break;
                }
            }
        }
        
        if (!string.IsNullOrEmpty(message?.Text) && message.Text =="/load")
        {
            if (_surveyStates.ContainsKey(message.Chat.Id))
            {
                _surveyStates[message.Chat.Id] = new SurveyState { CurrentStep = CargoStep.Number };
            }
            else
            {
                _surveyStates.Add(message.Chat.Id, new SurveyState { CurrentStep = CargoStep.Number });
            }
        }

        if (!_surveyStates.ContainsKey(message.Chat.Id))
        {
            _surveyStates[message.Chat.Id] = new SurveyState
            {
                CurrentStep = CargoStep.Number
            };

            await botClient.SendMessage(message.Chat.Id, "Введите номер груза",
                cancellationToken: cancellationToken);

            return;
        }

        var currentStep = _surveyStates[message.Chat.Id];

        switch (currentStep.CurrentStep)
        {
            case CargoStep.Number:
                await HandleNumberAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.Dispatcher:
                await HandleDispatcherAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.MC:
                await HandleMcAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.MileWithCargo:
                await HandleWithMileAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.WithoutCargo:
                await HandleMileInputAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.CostCargo:
                await HandleCostCargoAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.PathTravel:
                await HandlePathTravelAsync(botClient, message, currentStep, cancellationToken);
                break;
            case CargoStep.ChangeStep:
                await HandleChangeStepAsync(botClient, message, currentStep, cancellationToken);
                break;
        }
    }

    private async Task HandleWhatChangeAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.Text))
        {
            if (message.Text == "MC#")
            {
                // state.CurrentStep = CargoStep.MC;
                //
                // await botClient.SendMessage(message.Chat.Id, "Введите MC#", cancellationToken: cancellationToken);
                await HandleMcAsync(botClient, message, state, cancellationToken, true);
            }
            if (message.Text == "")
            {
                
            }
        }
    }

    private async Task HandleChangeStepAsync(ITelegramBotClient botClient, Message message, SurveyState state, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.Text))
        {
            if (message.Text.ToLower() == "нет")
            {
                Cargo cargo = new()
                {
                    Id = Guid.NewGuid(),
                    Number = _surveyStates[message.Chat.Id].Number,
                    CostCargo = _surveyStates[message.Chat.Id].CostCargo,
                    PathTravel = _surveyStates[message.Chat.Id].PathTravel,
                    DispatcherId = _surveyStates[message.Chat.Id].Dispatcher,
                    MC = _surveyStates[message.Chat.Id].Mc,
                    WithMile = _surveyStates[message.Chat.Id].MileWithCargo,
                    WithoutMile = _surveyStates[message.Chat.Id].MileWithoutCargo,
                };

                await _cargoService.AddAsync(cargo);
                _surveyStates.Remove(message.Chat.Id);
                
                await botClient.SendMessage(message.Chat.Id, "Груз добавлен", cancellationToken: cancellationToken);
            }

            if (message.Text.ToLower() == "да")
            {
                //todo разобраться с водилой и MC#
                
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("Номер груза", $"change-number"),
                    InlineKeyboardButton.WithCallbackData("Диспетчера", $"change-dispatcher"),
                    // InlineKeyboardButton.WithCallbackData("Водителя", $"change-"),
                    InlineKeyboardButton.WithCallbackData("MC# компании", $"change-mc"),
                    InlineKeyboardButton.WithCallbackData("Сколько миль пустым", $"change-without mile"),
                    InlineKeyboardButton.WithCallbackData("Сколько миль с грузом", $"change-mile withcargo"),
                    InlineKeyboardButton.WithCallbackData("Маршрут", $"change-path"),
                });

                await botClient.SendMessage(message.Chat.Id, "Что хотите изменить?", replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleNumberAsync(ITelegramBotClient botClient, Message message, SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.Number = message.Text;
            // surveyState.CurrentStep = CargoStep.Dispatcher;

            var lines = await _dispatcherService.GetAllAsync();

            var firstLine = lines.Select(x => x.Name).Take(lines.Count() / 2).ToArray();
            var secondLine = lines.Select(x => x.Name).Skip(lines.Count() / 2).ToArray();
            
            var inlineKeyboard = new InlineKeyboardButton[firstLine.Length][];

            for (int i = 0; i < firstLine.Length; i++)
            {
                var secondButton = i < secondLine.Length 
                    ? InlineKeyboardButton.WithCallbackData(secondLine[i], $"dispatcher-{secondLine[i]}") 
                    : null;
    
                inlineKeyboard[i] = new[]
                {
                    InlineKeyboardButton.WithCallbackData(firstLine[i], $"dispatcher-{firstLine[i]}"),
                    secondButton
                }.Where(b => b != null).ToArray();
            }

            var replyMarkup = new InlineKeyboardMarkup(inlineKeyboard);
            
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Введите дистпетчера:",
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleDispatcherAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            var dispatcher = await _dispatcherService.GetByNameAsync(message.Text);
            
            // surveyState.Dispatcher = dispatcher.Id.ToString();
            surveyState.CurrentStep = CargoStep.MC;
            
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Введите MC# компании:",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandleMcAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken, bool changStep = false)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.Mc = message.Text;

            if (changStep)
            {
                surveyState.CurrentStep = CargoStep.None;
            }
            else
            {
                surveyState.CurrentStep = CargoStep.WithoutCargo;
            
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите сколько миль пустым:",
                    cancellationToken: cancellationToken);
            }
        }
    }
    
    private async Task HandleMileInputAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            if (double.TryParse(message.Text, out double mile))
            {
                surveyState.MileWithoutCargo = mile;
                surveyState.CurrentStep = CargoStep.MileWithCargo;
            
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите сколько миль с грузом:",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(message.Chat.Id, "Введите корректные данные!",
                    cancellationToken: cancellationToken);
            }
        }
    }
    
    private async Task HandleWithMileAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            if (double.TryParse(message.Text, out double mile))
            {
                surveyState.MileWithCargo = mile;
                surveyState.CurrentStep = CargoStep.CostCargo;
            
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Введите сколько платят за груз:",
                    cancellationToken: cancellationToken);
            }
        }
    }
    
    private async Task HandleCostCargoAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (double.TryParse(message.Text, out double costCargo))
        {
            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                surveyState.CostCargo = costCargo;
                surveyState.CurrentStep = CargoStep.PathTravel;
            
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Маршрут: из какого штата/города \u2192 в какой штат/город:",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(message.Chat.Id, "Введите корректные данные!",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandlePathTravelAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.PathTravel = message.Text;
            surveyState.CurrentStep = CargoStep.ChangeStep;

            await botClient.SendMessage(message.Chat.Id, $"Хотите ли изменить груз?",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleNameDriver(ITelegramBotClient botClient, Message message, DriverState state,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.Text))
        {
            state.Step = DriverStep.McNumber;
            state.Name = message.Text;

            await botClient.SendMessage(message.Chat.Id, "Введите MC#", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleMCNumber(ITelegramBotClient botClient, Message message, DriverState state,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.Text))
        {
            state.Step = DriverStep.None;
            state.McNumber = message.Text;

            Driver driver = new()
            {
                Id = Guid.NewGuid(),
                Name = state.Name,
                MCNumber = state.McNumber
            };
            
            await _driverService.AddAsync(driver);
            
            await botClient.SendMessage(message.Chat.Id, "Водитель добавлен", cancellationToken: cancellationToken);
        }
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