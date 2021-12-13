using System;
using System.Threading.Tasks;
using VirtoCommerce.TaxModule.Core.Model;

namespace VirtoCommerce.TaxModule.Core.Services
{
    /// <summary>
    /// This interface should implement <see cref="ICrudService<TaxProvider>"/> without methods.
    /// Methods left for compatibility and should be removed after upgrade to inheritance
    /// </summary>
    public interface ITaxProviderService
    {
        [Obsolete(@"Need to remove after inherit ITaxProviderService from ICrudService<TaxProvider>.")]
        Task<TaxProvider[]> GetByIdsAsync(string[] ids, string responseGroup);
        [Obsolete(@"Need to remove after inherit ITaxProviderService from ICrudService<TaxProvider>.")]
        Task<TaxProvider> GetByIdAsync(string id, string responseGroup);
        [Obsolete(@"Need to remove after inherit ITaxProviderService from ICrudService<TaxProvider>.")]
        Task SaveChangesAsync(TaxProvider[] taxProviders);
        [Obsolete(@"Need to remove after inherit ITaxProviderService from ICrudService<TaxProvider>.")]
        Task DeleteAsync(string[] ids);
    }
}
