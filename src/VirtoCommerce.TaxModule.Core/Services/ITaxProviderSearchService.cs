using System;
using System.Threading.Tasks;
using VirtoCommerce.TaxModule.Core.Model.Search;

namespace VirtoCommerce.TaxModule.Core.Services
{
    /// <summary>
    /// This interface should implement <see cref="SearchService<TaxProvider>"/> without methods.
    /// Methods left for compatibility and should be removed after upgrade to inheritance
    /// </summary>
    public interface ITaxProviderSearchService
    {
        [Obsolete(@"Need to remove after inherit ITaxProviderSearchService from SearchService<TaxProvider>.")]
        Task<TaxProviderSearchResult> SearchTaxProvidersAsync(TaxProviderSearchCriteria criteria);
    }
}
