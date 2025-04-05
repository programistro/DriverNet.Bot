using DriverNet.Core.Models;

namespace DriverNet.Application.Interface;

public interface ICargoService
{
    Task<Cargo> GetByIdAsync(Guid numberId);
    
    Task<IEnumerable<Cargo>> GetAllAsync();
    
    Task<Cargo> GetByNumberAsync(string number);
    
    Task AddAsync(Cargo cargo);
    
    Task UpdateAsync(Cargo cargo);
    
    Task DeleteAsync(Guid guid);
}