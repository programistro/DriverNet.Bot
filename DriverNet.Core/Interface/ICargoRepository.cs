using DriverNet.Core.Models;

namespace DriverNet.Core.Interface;

public interface ICargoRepository
{
    Task<Cargo> GetByIdAsync(Guid cargoId);
    
    Task<Cargo> GetByNumberAsync(string number);
    
    Task Add(Cargo cargo);
    
    Task Update(Cargo cargo);
    
    Task Delete(Guid cargoId);
    
    Task<IEnumerable<Cargo>> GetAllAsync();
}