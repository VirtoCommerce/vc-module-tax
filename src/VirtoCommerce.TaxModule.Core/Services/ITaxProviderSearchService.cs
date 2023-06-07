using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;

namespace VirtoCommerce.TaxModule.Core.Services
{
    public interface ITaxProviderSearchService : ISearchService<TaxProviderSearchCriteria, TaxProviderSearchResult, TaxProvider>
    {
    }
}
