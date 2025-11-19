using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.TaxModule.Core.Model.Search
{
    public class TaxProviderSearchCriteria : SearchCriteriaBase
    {
        public string[] StoreIds { get; set; }
        /// <summary>
        /// Search only within tax providers that have changes and persisted
        /// </summary>
        public bool WithoutTransient { get; set; } = false;
    }
}
