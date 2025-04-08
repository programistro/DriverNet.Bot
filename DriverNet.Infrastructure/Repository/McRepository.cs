using DriverNet.Core.Interface;
using DriverNet.Core.Models;
using DriverNet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverNet.Infrastructure.Repository;

public class McRepository : IMcRepository
{
    private readonly AppDbContext _context;

    public McRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<McModel> GetByIdAsync(Guid mcId)
    {
        var mcModel = await _context.McModels.FirstOrDefaultAsync(x => x.Id == mcId);

        if (mcModel == null)
        {
            return null;
        }
        
        return mcModel;
    }

    public async Task<McModel> GetByNameAsync(string name)
    {
        var mcModel = await _context.McModels.FirstOrDefaultAsync(x => x.Name == name);

        if (mcModel == null)
        {
            return null;
        }
        
        return mcModel;
    }

    public async Task AddAsync(McModel mcModel)
    {
        _context.McModels.Add(mcModel);
        await _context.SaveChangesAsync();
    }

    public async Task Update(McModel mcModel)
    {
        _context.McModels.Update(mcModel);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(Guid mcId)
    {
        var mcModel = await _context.McModels.FirstOrDefaultAsync(x => x.Id == mcId);

        if (mcModel != null)
        {
            _context.McModels.Remove(mcModel);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<McModel>> GetAllAsync()
    {
        return await _context.McModels.ToListAsync();
    }
}