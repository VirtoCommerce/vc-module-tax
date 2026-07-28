using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.TaxModule.Data.Model;
using VirtoCommerce.TaxModule.Data.Repositories;

namespace VirtoCommerce.TaxModule.Data.Services
{
    public class TaxProviderSearchService : SearchService<TaxProviderSearchCriteria, TaxProviderSearchResult, TaxProvider, StoreTaxProviderEntity>, ITaxProviderSearchService
    {
        protected const string DefaultSortColumn = nameof(StoreTaxProviderEntity.Code);

        private readonly ISettingsManager _settingManager;

        public TaxProviderSearchService(
            Func<ITaxRepository> repositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            ITaxProviderService crudService,
            IOptions<CrudOptions> crudOptions,
            ISettingsManager settingManager)
            : base(repositoryFactory, platformMemoryCache, crudService, crudOptions)
        {
            _settingManager = settingManager;
        }

        protected override async Task<TaxProviderSearchResult> ProcessSearchResultAsync(TaxProviderSearchResult result, TaxProviderSearchCriteria criteria)
        {
            var sortInfos = BuildSortExpression(criteria);

            if (criteria.Take > 0 && !criteria.WithoutTransient)
            {
                // Not .AsQueryable().Where(...): that recompiles the expression tree per enumeration, a lock convoy under load.
                var transientProviders = AbstractTypeFactory<TaxProvider>.AllTypeInfos
                    .Select(x => AbstractTypeFactory<TaxProvider>.TryCreateInstance(x.Type.Name));

                if (!string.IsNullOrEmpty(criteria.Keyword))
                {
                    transientProviders = transientProviders.Where(x => x.Code.Contains(criteria.Keyword));
                }

                var persistentProviderTypes = result.Results.Select(x => x.GetType()).ToHashSet();
                var filteredTransientProviders = transientProviders
                    .Where(x => !persistentProviderTypes.Contains(x.GetType()))
                    .ToList();

                result.TotalCount += filteredTransientProviders.Count;

                var pagedTransientProviders = filteredTransientProviders
                    .Skip(criteria.Skip)
                    .Take(criteria.Take)
                    .ToList();

                foreach (var transientProvider in pagedTransientProviders)
                {
                    await _settingManager.DeepLoadSettingsAsync(transientProvider);
                }

                var allProviders = result.Results.Concat(pagedTransientProviders);

                // Arbitrary sort columns (admin, cold) are worth OrderBySortInfos' compile; the default
                // order is not. Decided from what BuildSortExpression returned, so overriding that seam
                // still changes the sort.
                result.Results = IsSingleAscendingDefaultSort(sortInfos)
                    ? allProviders.OrderBy(x => x.Code).ThenBy(x => x.Id).ToList()
                    : allProviders.AsQueryable().OrderBySortInfos(sortInfos).ThenBy(x => x.Id).ToList();
            }

            return result;
        }

        protected override IQueryable<StoreTaxProviderEntity> BuildQuery(IRepository repository, TaxProviderSearchCriteria criteria)
        {
            var query = ((ITaxRepository)repository).TaxProviders;

            if (!string.IsNullOrEmpty(criteria.Keyword))
            {
                query = query.Where(x => x.Code.Contains(criteria.Keyword) || x.Id.Contains(criteria.Keyword));
            }

            if (!criteria.StoreIds.IsNullOrEmpty())
            {
                query = query.Where(x => criteria.StoreIds.Contains(x.StoreId));
            }

            return query;
        }

        protected override IList<SortInfo> BuildSortExpression(TaxProviderSearchCriteria criteria)
        {
            var sortInfos = criteria.SortInfos;

            if (sortInfos.IsNullOrEmpty())
            {
                sortInfos = new[]
                {
                    new SortInfo
                    {
                        SortColumn = DefaultSortColumn
                    }
                };
            }

            return sortInfos;
        }

        protected static bool IsSingleAscendingDefaultSort(IList<SortInfo> sortInfos)
        {
            return sortInfos?.Count == 1
                && sortInfos[0].SortDirection == SortDirection.Ascending
                && DefaultSortColumn.EqualsIgnoreCase(sortInfos[0].SortColumn);
        }
    }
}
