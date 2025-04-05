using System.ComponentModel.DataAnnotations;

namespace DriverNet.Core.Models;

public class Driver
{
    [Key]
    public Guid Id { get; set; }
    
    public string Name { get; set; }
    
    public string MCNumber { get; set; }
}