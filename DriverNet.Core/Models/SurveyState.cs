namespace DriverNet.Core.Models;

public class SurveyState
{
    public string Number { get; set; }
    
    public string Mc { get; set; }
    
    public string Dispatcher { get; set; }
    
    /// <summary>
    /// миль без груза
    /// </summary>
    public double MileWithoutCargo { get; set; }
    
    /// <summary>
    /// миль с грузом
    /// </summary>
    public double MileWithCargo { get; set; }
    
    /// <summary>
    /// сколько платят за груз
    /// </summary>
    public double CostCargo { get; set; }
    
    /// <summary>
    /// маршрут: из какого штата/города → в какой штат/город
    /// </summary>
    public string PathTravel { get; set; }
    
    public SurveyStep CurrentStep { get; set; }
}