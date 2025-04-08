using DriverNet.Application.Interface;
using DriverNet.Core.Interface;
using DriverNet.Core.Models;

namespace DriverNet.Application.Service;

public class McService : IMcService
{
    private readonly IMcRepository _mcRepository;

    public McService(IMcRepository mcRepository)
    {
        _mcRepository = mcRepository;
    }
    
    public async Task<McModel> GetByIdAsync(Guid mcId)
    {
        return await _mcRepository.GetByIdAsync(mcId);
    }

    public async Task<IEnumerable<McModel>> GetAllAsync()
    {
        return await _mcRepository.GetAllAsync();
    }

    public async Task<McModel> GetByNameAsync(string name)
    {
        return await _mcRepository.GetByNameAsync(name);
    }

    public async Task AddAsync(McModel mcModel)
    {
        await _mcRepository.AddAsync(mcModel);
    }

    public async Task UpdateAsync(McModel mcModel)
    {
        await _mcRepository.Update(mcModel);
    }

    public async Task DeleteAsync(Guid guid)
    {
        await _mcRepository.Delete(guid);
    }
}