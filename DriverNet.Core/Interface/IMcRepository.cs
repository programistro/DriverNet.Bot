using DriverNet.Core.Models;

namespace DriverNet.Core.Interface;

public interface IMcRepository
{
    Task<McModel> GetByIdAsync(Guid mcId);
    
    Task<McModel> GetByNameAsync(string name);
    
    Task AddAsync(McModel mcModel);
    
    Task Update(McModel mcModel);
    
    Task Delete(Guid mcId);
    
    Task<IEnumerable<McModel>> GetAllAsync();
}