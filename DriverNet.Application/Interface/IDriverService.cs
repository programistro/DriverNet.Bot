using DriverNet.Core.Models;

namespace DriverNet.Application.Interface;

public interface IDriverService
{
    Task<Driver> GetByIdAsync(Guid driverId);
    
    Task<IEnumerable<Driver>> GetAllAsync();
    
    Task<Driver> GetByNameAsync(string name);
    
    Task AddAsync(Driver driver);
    
    Task UpdateAsync(Driver driver);
    
    Task DeleteAsync(Guid guid);
}