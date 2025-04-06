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
    private static DriverState _driverState = new();

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
        var message = update.Message;

        if (message?.Type != MessageType.Text || string.IsNullOrWhiteSpace(message?.Text))
            return;

        if (message.Chat.Id == -4713702986)
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
        
        if (message.Text =="/load")
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
        }
    }

    private async Task HandleNumberAsync(ITelegramBotClient botClient, Message message, SurveyState surveyState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            surveyState.Number = message.Text;
            surveyState.CurrentStep = CargoStep.Dispatcher;

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
            surveyState.CurrentStep = CargoStep.MC;
            
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
            surveyState.CurrentStep = CargoStep.WithoutCargo;
            
            await botClient.SendMessage(
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
            surveyState.CurrentStep = CargoStep.None;
            
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
            
            _surveyStates.Remove(message.Chat.Id);
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
            
            await _driverRepository.AddAsync(driver);
            
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