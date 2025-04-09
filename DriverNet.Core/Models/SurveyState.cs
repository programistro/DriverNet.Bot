namespace DriverNet.Core.Models;

public class SurveyState
{
    public CargoStep CurrentStep { get; set; }
    
    public bool IsEditing { get; set; }
    
    public string Number { get; set; }
    
    public string DispatcherId { get; set; }
    
    public string DriverId { get; set; }
    
    public string McId { get; set; }
    
    public double MileWithoutCargo { get; set; }
    
    public double MileWithCargo { get; set; }
    
    public double CostCargo { get; set; }
    
    public string PathTravel { get; set; }
}