using DriverNet.Core.Models;

namespace DriverNet.Application.Interface;

public interface IDispatcherService
{
    Task<Dispatcher> GetByIdAsync(Guid dispatcherId);
    
    Task<IEnumerable<Dispatcher>> GetAllAsync();
    
    Task<Dispatcher> GetByNameAsync(string name);
    
    Task AddAsync(Dispatcher dispatcher);
    
    Task UpdateAsync(Dispatcher dispatcher);
    
    Task DeleteAsync(Guid guid);
}