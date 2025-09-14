using BaseConhecimento.Models.Auth;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Knowledge;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BaseConhecimento.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Chamado> Chamados => Set<Chamado>();
        public DbSet<KnowledgeItem> KnowledgeBase => Set<KnowledgeItem>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<KnowledgeItem>(e =>
            {
                e.Property(p => p.Categoria).HasMaxLength(120).IsRequired();
                e.Property(p => p.Subcategoria).HasMaxLength(200).IsRequired();
                e.Property(p => p.Conteudo).IsRequired();
                e.Property(p => p.PerguntasFrequentes).HasMaxLength(4000);
                e.Property(p => p.EmbeddingJson).HasColumnType("nvarchar(max)");
            });
        }
    }
}
