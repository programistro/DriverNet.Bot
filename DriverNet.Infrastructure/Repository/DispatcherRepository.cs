using DriverNet.Core.Interface;
using DriverNet.Core.Models;
using DriverNet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverNet.Infrastructure.Repository;

public class DispatcherRepository : IDispatcherRepository
{
    private readonly AppDbContext _context;

    public DispatcherRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<Dispatcher> GetByIdAsync(Guid dispatcherId)
    {
        var dispatcher = await _context.Dispatchers.FirstOrDefaultAsync(x => x.Id == dispatcherId);

        if (dispatcher == null)
        {
            return null;
        }
        
        return dispatcher;
    }

    public async Task<Dispatcher> GetByNameAsync(string name)
    {
        var dispatcher = await _context.Dispatchers.FirstOrDefaultAsync(x => x.Name == name);

        if (dispatcher == null)
        {
            return null;
        }
        
        return dispatcher;
    }

    public async Task Add(Dispatcher dispatcher)
    {
        await _context.Dispatchers.AddAsync(dispatcher);
        await _context.SaveChangesAsync();
    }

    public async Task Update(Dispatcher dispatcher)
    {
        _context.Dispatchers.Update(dispatcher);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(Guid dispatcherId)
    {
        var dispatcher = await _context.Dispatchers.FirstOrDefaultAsync(x => x.Id == dispatcherId);

        if (dispatcher != null)
        {
            _context.Dispatchers.Remove(dispatcher);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Dispatcher>> GetAllAsync()
    {
        return await _context.Dispatchers.ToListAsync();
    }
}