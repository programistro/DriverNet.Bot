namespace DriverNet.Core.Models;

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