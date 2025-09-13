// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using BaseConhecimento.Models.Chamados; // ajuste se seu namespace for diferente

namespace BaseConhecimento.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Chamado> Chamados => Set<Chamado>();
    }
}
