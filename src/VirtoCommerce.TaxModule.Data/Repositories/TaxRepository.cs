using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.TaxModule.Data.Model;

namespace VirtoCommerce.TaxModule.Data.Repositories
{
    public class TaxRepository : DbContextRepositoryBase<TaxDbContext>, ITaxRepository
    {
        public TaxRepository(TaxDbContext dbContext) : base(dbContext)
        {
        }

        public IQueryable<StoreTaxProviderEntity> TaxProviders => DbContext.Set<StoreTaxProviderEntity>();

        public Task<IEnumerable<StoreTaxProviderEntity>> GetByIdsAsync(IEnumerable<string> ids)
        {
            return Task.FromResult<IEnumerable<StoreTaxProviderEntity>>(TaxProviders.Where(x => ids.Contains(x.Id)).ToList());
        }
    }
}
