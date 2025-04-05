using DriverNet.Core.Models;

namespace DriverNet.Core.Interface;

public interface IDriverRepository
{
    Task<Driver> GetByIdAsync(Guid driverId);
    
    Task<Driver> GetByNameAsync(string name);
    
    Task AddAsync(Driver driver);
    
    Task Update(Driver driver);
    
    Task Delete(Guid driverId);
    
    Task<IEnumerable<Driver>> GetAllAsync();
}