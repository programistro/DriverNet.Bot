using DriverNet.Application.Interface;
using DriverNet.Core.Interface;
using DriverNet.Core.Models;

namespace DriverNet.Application.Service;

public class DispatcherService : IDispatcherService
{
    private readonly IDispatcherRepository _dispatcherRepository;

    public DispatcherService(IDispatcherRepository dispatcherRepository)
    {
        _dispatcherRepository = dispatcherRepository;
    }
    
    public async Task<Dispatcher> GetByIdAsync(Guid dispatcherId)
    {
        return await _dispatcherRepository.GetByIdAsync(dispatcherId);
    }

    public async Task<IEnumerable<Dispatcher>> GetAllAsync()
    {
        return await _dispatcherRepository.GetAllAsync();
    }

    public async Task<Dispatcher> GetByNameAsync(string name)
    {
        return await _dispatcherRepository.GetByNameAsync(name);
    }

    public async Task AddAsync(Dispatcher dispatcher)
    {
        await _dispatcherRepository.AddAsync(dispatcher);
    }

    public async Task UpdateAsync(Dispatcher dispatcher)
    {
        await _dispatcherRepository.Update(dispatcher);
    }

    public async Task DeleteAsync(Guid guid)
    {
        await _dispatcherRepository.Delete(guid);
    }
}