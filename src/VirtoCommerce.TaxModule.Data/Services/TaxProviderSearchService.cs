using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly ISettingsManager _settingManager;

        public TaxProviderSearchService(Func<ITaxRepository> repositoryFactory, IPlatformMemoryCache platformMemoryCache, ITaxProviderService taxProviderService, ISettingsManager settingManager)
            : base(repositoryFactory, platformMemoryCache, (ICrudService<TaxProvider>)taxProviderService)
        {
            _settingManager = settingManager;
        }

        protected override async Task<TaxProviderSearchResult> ProcessSearchResultAsync(TaxProviderSearchResult result, TaxProviderSearchCriteria criteria)
        {
            var sortInfos = BuildSortExpression(criteria);

            if (criteria.Take > 0 && !criteria.WithoutTransient)
            {
                var transientProvidersQuery = AbstractTypeFactory<TaxProvider>.AllTypeInfos.Select(x => AbstractTypeFactory<TaxProvider>.TryCreateInstance(x.Type.Name))
                                                                              .OfType<TaxProvider>().AsQueryable();
                if (!string.IsNullOrEmpty(criteria.Keyword))
                {
                    transientProvidersQuery = transientProvidersQuery.Where(x => x.Code.Contains(criteria.Keyword));
                }
                var allPersistentProvidersTypes = result.Results.Select(x => x.GetType()).Distinct();
                transientProvidersQuery = transientProvidersQuery.Where(x => !allPersistentProvidersTypes.Contains(x.GetType()));

                result.TotalCount += transientProvidersQuery.Count();
                var transientProviders = transientProvidersQuery.Skip(criteria.Skip)
                                                                .Take(criteria.Take)
                                                                .ToList();

                foreach (var transientProvider in transientProviders)
                {
                    await _settingManager.DeepLoadSettingsAsync(transientProvider);
                }

                result.Results = result.Results.Concat(transientProviders).AsQueryable()
                                               .OrderBySortInfos(sortInfos).ThenBy(x => x.Id).ToList();
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
            if (!criteria.StoreId.IsNullOrEmpty())
            {
                query = query.Where(x => x.StoreId == criteria.StoreId);
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
                        SortColumn = nameof(StoreTaxProviderEntity.Code)
                    }
                };
            }
            return sortInfos;
        }

        public async Task<TaxProviderSearchResult> SearchTaxProvidersAsync(TaxProviderSearchCriteria criteria)
        {
            return await SearchAsync(criteria);
        }
    }
}
