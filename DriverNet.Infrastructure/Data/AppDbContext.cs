using DriverNet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DriverNet.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Driver> Drivers { get; set; }
    
    public DbSet<Dispatcher> Dispatchers { get; set; }
    
    public DbSet<Cargo> Cargos { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=database.db");
    }
}