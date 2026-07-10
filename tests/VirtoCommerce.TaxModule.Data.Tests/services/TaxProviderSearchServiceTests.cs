using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.TaxModule.Data.Repositories;
using VirtoCommerce.TaxModule.Data.Services;
using Xunit;

namespace VirtoCommerce.TaxModule.Data.Tests.Services
{
    /// <summary>
    /// Covers the transient-provider shaping in <see cref="TaxProviderSearchService.ProcessSearchResultAsync"/>:
    /// AbstractTypeFactory-registered providers are materialized, keyword-filtered, deduplicated against
    /// persisted providers, counted, and sorted. These are the invariants the VCST-5303 rewrite (plain
    /// LINQ-to-objects instead of EnumerableQuery composition) had to preserve.
    /// </summary>
    public class TaxProviderSearchServiceTests
    {
        // ProcessSearchResultAsync turns every AbstractTypeFactory<TaxProvider>-registered type into a
        // transient provider, so these tests depend on the registered set. The factory is process-global
        // static; the isolation is dedicated probe types registered exactly once here — no other code in
        // this test project registers TaxProvider, so the transient set is deterministically {One, Two, Three}.
        static TaxProviderSearchServiceTests()
        {
            AbstractTypeFactory<TaxProvider>.RegisterType<ProbeOneTaxProvider>();
            AbstractTypeFactory<TaxProvider>.RegisterType<ProbeTwoTaxProvider>();
            AbstractTypeFactory<TaxProvider>.RegisterType<ProbeThreeTaxProvider>();
        }

        [Fact]
        public async Task ProcessSearchResult_EmptyPersisted_MaterializesAllTransientProviders()
        {
            var service = CreateService();
            var result = new TaxProviderSearchResult();
            var criteria = new TaxProviderSearchCriteria { Take = 20 };

            await service.RunProcessSearchResultAsync(result, criteria);

            Assert.Equal(3, result.Results.Count);
            Assert.Equal(3, result.TotalCount);
            Assert.Contains(result.Results, x => x is ProbeOneTaxProvider);
            Assert.Contains(result.Results, x => x is ProbeTwoTaxProvider);
            Assert.Contains(result.Results, x => x is ProbeThreeTaxProvider);
        }

        [Fact]
        public async Task ProcessSearchResult_WithKeyword_KeepsOnlyMatchingTransientProviders()
        {
            var service = CreateService();
            var result = new TaxProviderSearchResult();
            var criteria = new TaxProviderSearchCriteria { Take = 20, Keyword = "aaa" };

            await service.RunProcessSearchResultAsync(result, criteria);

            var provider = Assert.Single(result.Results);
            Assert.Equal("aaa", provider.Code);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task ProcessSearchResult_PersistedProviderTypePresent_ExcludesSameTypeTransient()
        {
            var service = CreateService();
            var persisted = new ProbeTwoTaxProvider { Id = "persisted-two" };
            var result = new TaxProviderSearchResult
            {
                Results = new List<TaxProvider> { persisted },
                TotalCount = 1,
            };
            var criteria = new TaxProviderSearchCriteria { Take = 20 };

            await service.RunProcessSearchResultAsync(result, criteria);

            // The persisted ProbeTwo wins its slot; the transient ProbeTwo is dropped. Only One and Three
            // are added transiently. TotalCount = 1 persisted + 2 transient.
            var twoProviders = result.Results.Where(x => x is ProbeTwoTaxProvider).ToList();
            Assert.Single(twoProviders);
            Assert.Same(persisted, twoProviders[0]);
            Assert.Equal(3, result.Results.Count);
            Assert.Equal(3, result.TotalCount);
        }

        [Fact]
        public async Task ProcessSearchResult_IncrementsTotalCountByTransientCount()
        {
            var service = CreateService();
            // Simulate a persisted total larger than the current page (Results empty → no dedup).
            var result = new TaxProviderSearchResult { TotalCount = 5 };
            var criteria = new TaxProviderSearchCriteria { Take = 20 };

            await service.RunProcessSearchResultAsync(result, criteria);

            Assert.Equal(8, result.TotalCount);
        }

        [Fact]
        public async Task ProcessSearchResult_DefaultSort_OrdersByCodeThenById()
        {
            var service = CreateService();
            // Two persisted providers share a Code to exercise the ThenBy(Id) tie-breaker; their types
            // also suppress the matching transient providers, leaving only the transient "aaa" (ProbeTwo).
            var result = new TaxProviderSearchResult
            {
                Results = new List<TaxProvider>
                {
                    new ProbeOneTaxProvider { Id = "z", Code = "mmm" },
                    new ProbeThreeTaxProvider { Id = "a", Code = "mmm" },
                },
                TotalCount = 2,
            };
            var criteria = new TaxProviderSearchCriteria { Take = 20 };

            await service.RunProcessSearchResultAsync(result, criteria);

            var ordered = result.Results.Select(x => (x.Code, x.Id)).ToList();
            Assert.Equal(new[] { ("aaa", null), ("mmm", "a"), ("mmm", "z") }, ordered);
        }

        [Fact]
        public async Task ProcessSearchResult_ExplicitSort_UsesRequestedDirection()
        {
            var service = CreateService();
            var result = new TaxProviderSearchResult();
            var criteria = new TaxProviderSearchCriteria { Take = 20, Sort = "Code:desc" };

            await service.RunProcessSearchResultAsync(result, criteria);

            var codes = result.Results.Select(x => x.Code).ToList();
            Assert.Equal(new[] { "ccc", "bbb", "aaa" }, codes);
        }

        [Fact]
        public async Task ProcessSearchResult_WithoutTransient_LeavesResultUntouched()
        {
            var service = CreateService();
            var persisted = new ProbeOneTaxProvider { Id = "p1" };
            var result = new TaxProviderSearchResult
            {
                Results = new List<TaxProvider> { persisted },
                TotalCount = 1,
            };
            var criteria = new TaxProviderSearchCriteria { Take = 20, WithoutTransient = true };

            await service.RunProcessSearchResultAsync(result, criteria);

            var provider = Assert.Single(result.Results);
            Assert.Same(persisted, provider);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task ProcessSearchResult_TakeZero_LeavesResultUntouched()
        {
            var service = CreateService();
            var result = new TaxProviderSearchResult { TotalCount = 4 };
            var criteria = new TaxProviderSearchCriteria { Take = 0 };

            await service.RunProcessSearchResultAsync(result, criteria);

            Assert.Empty(result.Results);
            Assert.Equal(4, result.TotalCount);
        }

        private static TestableTaxProviderSearchService CreateService()
        {
            var settingsManager = new Mock<ISettingsManager>();
            // DeepLoadSettingsAsync (an extension method) walks each provider's settings via
            // GetSettingsForType; an empty descriptor list makes it a no-op so the tests exercise
            // only the shaping/sorting logic.
            settingsManager
                .Setup(x => x.GetSettingsForType(It.IsAny<string>()))
                .Returns([]);

            return new TestableTaxProviderSearchService(
                () => new Mock<ITaxRepository>().Object,
                new Mock<IPlatformMemoryCache>().Object,
                new Mock<ITaxProviderService>().Object,
                Options.Create(new CrudOptions()),
                settingsManager.Object);
        }

        private sealed class TestableTaxProviderSearchService : TaxProviderSearchService
        {
            public TestableTaxProviderSearchService(
                Func<ITaxRepository> repositoryFactory,
                IPlatformMemoryCache platformMemoryCache,
                ITaxProviderService crudService,
                IOptions<CrudOptions> crudOptions,
                ISettingsManager settingManager)
                : base(repositoryFactory, platformMemoryCache, crudService, crudOptions, settingManager)
            {
            }

            public Task<TaxProviderSearchResult> RunProcessSearchResultAsync(TaxProviderSearchResult result, TaxProviderSearchCriteria criteria)
            {
                return ProcessSearchResultAsync(result, criteria);
            }
        }

        public class ProbeOneTaxProvider : TaxProvider
        {
            public ProbeOneTaxProvider()
            {
                Code = "ccc";
            }

            public override IEnumerable<TaxRate> CalculateRates(IEvaluationContext context)
            {
                return [];
            }
        }

        public class ProbeTwoTaxProvider : TaxProvider
        {
            public ProbeTwoTaxProvider()
            {
                Code = "aaa";
            }

            public override IEnumerable<TaxRate> CalculateRates(IEvaluationContext context)
            {
                return [];
            }
        }

        public class ProbeThreeTaxProvider : TaxProvider
        {
            public ProbeThreeTaxProvider()
            {
                Code = "bbb";
            }

            public override IEnumerable<TaxRate> CalculateRates(IEvaluationContext context)
            {
                return [];
            }
        }
    }
}
