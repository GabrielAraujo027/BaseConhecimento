using BaseConhecimento.Models.Chamados; 
using BaseConhecimento.Models.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace BaseConhecimento.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Chamado> Chamados => Set<Chamado>();
        public DbSet<KnowledgeItem> KnowledgeBase => Set<KnowledgeItem>();
    }
}
