using System.Reflection;
using EntityFrameworkCore.Triggers;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.TaxModule.Data.Model;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.TaxModule.Data.Repositories
{
    public class TaxDbContext : DbContextBase
    {
        public TaxDbContext(DbContextOptions<TaxDbContext> options) : base(options)
        {
        }

        protected TaxDbContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region StoreTaxProvider
            modelBuilder.Entity<StoreTaxProviderEntity>().ToTable("StoreTaxProvider").HasKey(x => x.Id);
            modelBuilder.Entity<StoreTaxProviderEntity>().Property(x => x.Id).HasMaxLength(128).ValueGeneratedOnAdd();
            modelBuilder.Entity<StoreTaxProviderEntity>().Property(x => x.StoreId).HasMaxLength(128);

            modelBuilder.Entity<StoreTaxProviderEntity>().HasIndex(x => new { x.TypeName, x.StoreId })
                      .HasDatabaseName("IX_StoreTaxProviderEntity_TypeName_StoreId")
                      .IsUnique();
            #endregion

            base.OnModelCreating(modelBuilder);


            // Allows configuration for an entity type for different database types.
            // Applies configuration from all <see cref="IEntityTypeConfiguration{TEntity}" in VirtoCommerce.TaxModule.Data.XXX project. /> 
            switch (this.Database.ProviderName)
            {
                case "Pomelo.EntityFrameworkCore.MySql":
                    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.TaxModule.Data.MySql"));
                    break;
                case "Npgsql.EntityFrameworkCore.PostgreSQL":
                    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.TaxModule.Data.PostgreSql"));
                    break;
                case "Microsoft.EntityFrameworkCore.SqlServer":
                    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.TaxModule.Data.SqlServer"));
                    break;
            }

        }
    }
}
