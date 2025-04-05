using DriverNet.Core.Interface;
using DriverNet.Core.Models;
using DriverNet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverNet.Infrastructure.Repository;

public class DriverRepository : IDriverRepository
{
    private readonly AppDbContext _context;

    public DriverRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<Driver> GetByIdAsync(Guid driverId)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(x => x.Id == driverId);

        if (driver == null)
        {
            return null;
        }
        
        return driver;
    }

    public async Task<Driver> GetByNameAsync(string name)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(x => x.Name == name);

        if (driver == null)
        {
            return null;
        }
        
        return driver;
    }

    public async Task Add(Driver driver)
    {
        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();
    }

    public async Task Update(Driver driver)
    {
        _context.Drivers.Update(driver);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(Guid driverId)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(x => x.Id == driverId);

        if (driver != null)
        {
            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Driver>> GetAllAsync()
    {
        return await _context.Drivers.ToListAsync();
    }
}