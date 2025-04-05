using DriverNet.Application.Interface;
using DriverNet.Core.Interface;
using DriverNet.Core.Models;

namespace DriverNet.Application.Service;

public class DriverService : IDriverService
{
    private readonly IDriverRepository _repository;

    public DriverService(IDriverRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Driver> GetByIdAsync(Guid driverId)
    {
        return await _repository.GetByIdAsync(driverId);
    }

    public async Task<IEnumerable<Driver>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Driver> GetByNameAsync(string name)
    {
        return await _repository.GetByNameAsync(name);
    }

    public async Task AddAsync(Driver driver)
    {
        await _repository.AddAsync(driver);
    }

    public async Task UpdateAsync(Driver driver)
    {
        await _repository.Update(driver);
    }

    public async Task DeleteAsync(Guid guid)
    {
        await _repository.Delete(guid);
    }
}