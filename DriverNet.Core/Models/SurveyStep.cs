namespace DriverNet.Core.Models;

public enum SurveyStep
{
    None = 0,
    WaitingForNumber = 1,
    WaitingForDispatcher = 2,
    WaitingForMC = 3,
    /// <summary>
    /// миль без груза
    /// </summary>
    WaitingForMileWithoutCargo = 4,
    /// <summary>
    /// миль с грузом
    /// </summary>
    WaitingForMileWithCargo = 5,
    /// <summary>
    /// сколько платят за груз
    /// </summary>
    CostCargo = 6,
    /// <summary>
    /// маршрут: из какого штата/города → в какой штат/город
    /// </summary>
    PathTravel = 7
}