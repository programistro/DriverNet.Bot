using DriverNet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DriverNet.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Driver> Drivers => Set<Driver>();
    
    public DbSet<Dispatcher> Dispatchers => Set<Dispatcher>();
    
    public DbSet<Cargo> Cargos => Set<Cargo>();
    
    // public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("host=localhost;port=5432;Username=postgres;Password=post;Database=drivers");
    }
}