using System.ComponentModel.DataAnnotations;

namespace DriverNet.Core.Models;

public class McModel
{
    [Key]
    public Guid Id { get; set; }

    public string Name { get; set; }
}