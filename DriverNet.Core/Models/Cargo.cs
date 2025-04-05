using System.ComponentModel.DataAnnotations;

namespace DriverNet.Core.Models;

public class Cargo
{
    [Key] public Guid Id { get; set; }

    public string Number { get; set; }

    public string DispatcherId { get; set; }

    public string MC { get; set; }

    public double WithoutMile { get; set; }

    public double WithMile { get; set; }

    public double CostCargo { get; set; }

    public string PathTravel { get; set; }
}