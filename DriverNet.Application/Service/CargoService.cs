using DriverNet.Application.Interface;
using DriverNet.Core.Interface;
using DriverNet.Core.Models;

namespace DriverNet.Application.Service;

public class CargoService : ICargoService
{
    private readonly ICargoRepository _cargoRepository;

    public CargoService(ICargoRepository cargoRepository)
    {
        _cargoRepository = cargoRepository;
    }
    
    public async Task<Cargo> GetByIdAsync(Guid numberId)
    {
        return await _cargoRepository.GetByIdAsync(numberId);
    }

    public async Task<IEnumerable<Cargo>> GetAllAsync()
    {
        return await _cargoRepository.GetAllAsync();
    }

    public async Task<Cargo> GetByNumberAsync(string number)
    {
        return await _cargoRepository.GetByNumberAsync(number);
    }

    public async Task AddAsync(Cargo cargo)
    {
        await _cargoRepository.AddAsync(cargo);
    }

    public async Task UpdateAsync(Cargo cargo)
    {
        await _cargoRepository.Update(cargo);
    }

    public async Task DeleteAsync(Guid guid)
    {
        await _cargoRepository.Delete(guid);
    }
}