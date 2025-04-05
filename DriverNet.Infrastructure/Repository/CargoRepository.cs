using DriverNet.Core.Interface;
using DriverNet.Core.Models;
using DriverNet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverNet.Infrastructure.Repository;

public class CargoRepository : ICargoRepository
{
    private readonly AppDbContext _context;

    public CargoRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<Cargo> GetByIdAsync(Guid cargoId)
    {
        var cargo = await _context.Cargos.FirstOrDefaultAsync(x => x.Id == cargoId);

        if (cargo == null)
        {
            return null;
        }
        
        return cargo;
    }

    public async Task<Cargo> GetByNumberAsync(string number)
    {
        var cargo = await _context.Cargos.FirstOrDefaultAsync(x => x.Number == number);

        if (cargo == null)
        {
            return null;
        }
        
        return cargo;
    }

    public async Task AddAsync(Cargo cargo)
    {
        await _context.Cargos.AddAsync(cargo);
        await _context.SaveChangesAsync();
    }

    public async Task Update(Cargo cargo)
    {
        _context.Cargos.Update(cargo);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(Guid cargoId)
    {
        var cargo = await _context.Cargos.FirstOrDefaultAsync(x => x.Id == cargoId);

        if (cargo != null)
        {
            _context.Cargos.Remove(cargo);
            await _context.SaveChangesAsync();
        }   
    }

    public async Task<IEnumerable<Cargo>> GetAllAsync()
    {
        return await _context.Cargos.ToListAsync();
    }
}