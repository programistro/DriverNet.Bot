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
    private readonly IDriverRepository _driverRepository;
    private readonly IDispatcherRepository _dispatcherRepository;
    private readonly ICargoRepository _cargoRepository;
    private CancellationTokenSource _cts;
    private static Dictionary<long, SurveyState> _surveyStates = new();

    public TelegramBotService(
        string botToken,
        IDriverRepository driverRepository,
        IDispatcherRepository dispatcherRepository, ICargoRepository cargoRepository)
    {
        _botClient = new TelegramBotClient(botToken);
        _driverRepository = driverRepository;
        _dispatcherRepository = dispatcherRepository;
        _cargoRepository = cargoRepository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var me = await _botClient.GetMeAsync(cancellationToken);
        Console.WriteLine($"Bot {me.Username} started");
        
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
        var message = update.Message;

        if (message?.Type != MessageType.Text && !string.IsNullOrWhiteSpace(message?.Text))
        {
            if (message.Text.StartsWith("/driver-load"))
            {
                if (!_surveyStates.ContainsKey(message.Chat.Id))
                {
                    _surveyStates[message.Chat.Id] = new SurveyState
                    {
                        CurrentStep = SurveyStep.WaitingForNumber
                    };

                    await botClient.SendTextMessageAsync(message.Chat.Id, "Введите номер груза",
                        cancellationToken: cancellationToken);
                    
                    return;
                }
                
                var currentStep = _surveyStates[message.Chat.Id];

                switch (currentStep.CurrentStep)
                {
                    case SurveyStep.WaitingForNumber:
                        await HandleNumberAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.WaitingForDispatcher:
                        await HandleDispatcherAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.WaitingForMC:
                        await HandleMcAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.WaitingForMileWithCargo:
                        await HandleWithMileAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.WaitingForMileWithoutCargo:
                        await HandleMileInputAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.CostCargo:
                        await HandleCostCargoAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.PathTravel:
                        await HandlePathTrabelAsync(botClient, message, currentStep, cancellationToken);
                        break;
                    case SurveyStep.None:
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
                        
                        await _cargoRepository.AddAsync(cargo);

                        await botClient.SendMessage(message.Chat.Id, $"Груз добавлен",
                            cancellationToken: cancellationToken);
                        
                        break;
                }
            }
        }
    }

    private async Task HandleNumberAsync(ITelegramBotClient botClient, Message message, SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.Number = message.Text;
            surveyState.CurrentStep = SurveyStep.WaitingForDispatcher;

            var lines = await _dispatcherRepository.GetAllAsync();

            var firstLine = lines.Select(x => x.Name).Take(lines.Count() / 2).ToArray();
            var secondLine = lines.Select(x => x.Name).Skip(lines.Count() / 2).ToArray();
            
            var inlineMarkup = new string[][]
            {
                firstLine,
                secondLine
            };
            
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Введите дистпетчера:",
                replyMarkup: inlineMarkup,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleDispatcherAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            var dispatcher = _dispatcherRepository.GetByNameAsync(message.Text);
            
            surveyState.Dispatcher = dispatcher.Id.ToString();
            surveyState.CurrentStep = SurveyStep.WaitingForMC;
            
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Введите MC# компании:",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandleMcAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.Mc = message.Text;
            surveyState.CurrentStep = SurveyStep.WaitingForMileWithoutCargo;
            
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите сколько миль пустым:",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandleMileInputAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.MileWithoutCargo = double.Parse(message.Text);
            surveyState.CurrentStep = SurveyStep.WaitingForMileWithCargo;
            
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите сколько миль с грузом:",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandleWithMileAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.MileWithCargo = double.Parse(message.Text);
            surveyState.CurrentStep = SurveyStep.CostCargo;
            
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Введите сколько платят за груз:",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandleCostCargoAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.CostCargo = double.Parse(message.Text);
            surveyState.CurrentStep = SurveyStep.PathTravel;
            
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Маршрут: из какого штата/города \u2192 в какой штат/город:",
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandlePathTrabelAsync(ITelegramBotClient botClient, Message message,
        SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.PathTravel = message.Text;
            surveyState.CurrentStep = SurveyStep.None;
            
            // await botClient.SendTextMessageAsync(
            //     chatId: message.Chat.Id,
            //     text: "Маршрут: из какого штата/города \u2192 в какой штат/город:",
            //     cancellationToken: cancellationToken);
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