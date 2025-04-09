using System.ComponentModel.DataAnnotations;

namespace DriverNet.Core.Models;

public class Dispatcher
{
    [Key]
    public Guid Id { get; set; }
    
    public string Name { get; set; }
    
    public PercentDispatcher? PercentDispatcher { get; set; }
    
    public double Percent { get; set; }
}