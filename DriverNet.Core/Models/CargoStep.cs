namespace DriverNet.Core.Models;

public enum CargoStep
{
    None = 0,
    Number = 1,
    Dispatcher = 2,
    MC = 3,
    /// <summary>
    /// миль без груза
    /// </summary>
    WithoutCargo = 4,
    /// <summary>
    /// миль с грузом
    /// </summary>
    MileWithCargo = 5,
    /// <summary>
    /// сколько платят за груз
    /// </summary>
    CostCargo = 6,
    /// <summary>
    /// маршрут: из какого штата/города → в какой штат/город
    /// </summary>
    PathTravel = 7,
    ChangeStep = 8,
    WhatChange = 9,
}