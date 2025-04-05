using DriverNet.Core.Models;

namespace DriverNet.Core.Interface;

public interface IDispatcherRepository
{
    Task<Dispatcher> GetByIdAsync(Guid dispatcherId);
    
    Task<Dispatcher> GetByNameAsync(string name);
    
    Task Add(Dispatcher dispatcher);
    
    Task Update(Dispatcher dispatcher);
    
    Task Delete(Guid dispatcherId);
    
    Task<IEnumerable<Dispatcher>> GetAllAsync();
}