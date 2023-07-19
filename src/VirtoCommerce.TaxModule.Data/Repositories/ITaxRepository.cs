using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.TaxModule.Data.Model;

namespace VirtoCommerce.TaxModule.Data.Repositories
{
    public interface ITaxRepository : IRepository
    {
        IQueryable<StoreTaxProviderEntity> TaxProviders { get; }
        Task<IList<StoreTaxProviderEntity>> GetByIdsAsync(IList<string> ids);
    }
}
