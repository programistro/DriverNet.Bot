using DriverNet.Core.Models;

namespace DriverNet.Application.Interface;

public interface IMcService
{
    Task<McModel> GetByIdAsync(Guid mcId);
    
    Task<IEnumerable<McModel>> GetAllAsync();
    
    Task<McModel> GetByNameAsync(string name);
    
    Task AddAsync(McModel mcModel);
    
    Task UpdateAsync(McModel mcModel);
    
    Task DeleteAsync(Guid guid);
}