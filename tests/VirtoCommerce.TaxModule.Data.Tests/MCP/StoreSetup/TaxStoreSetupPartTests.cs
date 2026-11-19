using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.MCP.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Core.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.TaxModule.Data.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Data.Provider;
using Xunit;
using Store = VirtoCommerce.StoreModule.Core.Model.Store;

namespace VirtoCommerce.TaxModule.Data.Tests.MCP.StoreSetup;

public class TaxStoreSetupPartTests
{
    private const string StoreId = "eu-store";
    private const string FixedRateCode = "FixedRate";
    private const string TestCode = "TestTax";
    private const string TaxCalculationSetting = "Stores.TaxCalculationEnabled";

    private static readonly SettingDescriptor _rate = new() { Name = "Test.Tax.Rate", ValueType = SettingValueType.Decimal, DefaultValue = 0m };
    private static readonly SettingDescriptor _apiKey = new() { Name = "Test.Tax.ApiKey", ValueType = SettingValueType.ShortText };

    // The providers the store has saved; the search adds a new instance of every installed provider the store has not saved.
    private readonly List<TaxProvider> _saved = [];
    private readonly Mock<ITaxProviderService> _providerService = new();

    [Fact]
    public async Task GetChoicesAsync_ListsTheInstalledProviders_WithTheirSettingsThatAreNotSecrets()
    {
        //Act
        var choices = await CreatePart().GetChoicesAsync();

        //Assert
        Assert.Equal([FixedRateCode, TestCode], choices.Select(x => x.Value));
        Assert.All(choices, x => Assert.Equal("tax.provider", x.Field));
        Assert.Equal([_rate.Name], choices.Single(x => x.Value == TestCode).Settings.Select(x => x.Name));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CheckAsync_WithoutAnActiveProvider_WarnsOnlyWhileTaxCalculationIsOn(bool? taxCalculation, bool expectWarning)
    {
        //Arrange
        var store = new Store { Id = StoreId, Name = "EU store", Settings = [] };

        if (taxCalculation != null)
        {
            store.Settings.Add(new ObjectSettingEntry { Name = TaxCalculationSetting, Value = taxCalculation.Value });
        }

        //Act
        var findings = await CreatePart().CheckAsync(new StoreSetupContext { Store = store });

        //Assert
        Assert.Equal(expectWarning, findings.Any(x => x.Id == "tax-no-active-provider" && x.Severity == McpFindingSeverities.Warning));
    }

    // The cart takes the first active provider in the search's order, whatever the priorities say.
    [Fact]
    public async Task CheckAsync_WithSeveralActiveProviders_Warns_AndTheProviderInUseIsTheFirstInTheSearchOrder()
    {
        //Arrange
        _saved.Add(new FixedRateTaxProvider { StoreId = StoreId, IsActive = true, Priority = 2 });
        _saved.Add(new TestTaxProvider { StoreId = StoreId, IsActive = true, Priority = 1 });
        var part = CreatePart();

        //Act
        var findings = await part.CheckAsync(CreateContext());
        var state = (TaxStoreSetupSection)await part.GetStateAsync(CreateContext().Store);

        //Assert
        var finding = Assert.Single(findings);
        Assert.Equal("tax-several-active-providers", finding.Id);
        Assert.Contains($"{FixedRateCode}, {TestCode}", finding.Evidence);
        Assert.Equal(FixedRateCode, state.Provider);
    }

    // A setting the provider never saved has its default: sending that value changes nothing.
    [Fact]
    public async Task PlanAsync_ASettingSentWithTheValueInEffect_ChangesNothing()
    {
        //Arrange
        _saved.Add(new TestTaxProvider { StoreId = StoreId, IsActive = true });
        var section = new TaxStoreSetupSection { Provider = TestCode, Settings = new Dictionary<string, object> { [_rate.Name] = 0m } };

        //Act
        var plan = await CreatePart().PlanAsync(CreateContext(), section);

        //Assert
        Assert.Empty(plan.Errors);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public async Task PlanAsync_AProviderCode_ActivatesIt_AndSwitchesTheOthersOff()
    {
        //Arrange
        _saved.Add(new FixedRateTaxProvider { StoreId = StoreId, IsActive = true });
        var section = new TaxStoreSetupSection { Provider = TestCode, Settings = new Dictionary<string, object> { [_rate.Name] = 0.19m } };

        //Act
        var plan = await CreatePart().PlanAsync(CreateContext(), section);

        //Assert
        Assert.Empty(plan.Errors);
        Assert.Equal([$"providers.{FixedRateCode}.active", $"providers.{TestCode}.active", $"providers.{TestCode}.{_rate.Name}"], plan.Changes.Select(x => x.Field));
        Assert.Contains($"Switches off the tax provider {FixedRateCode}.", plan.Warnings);
        _providerService.Verify(x => x.SaveChangesAsync(It.IsAny<IList<TaxProvider>>()), Times.Never);
    }

    [Fact]
    public async Task PlanAsync_AnEmptyCode_SwitchesEveryProviderOff()
    {
        //Arrange
        _saved.Add(new FixedRateTaxProvider { StoreId = StoreId, IsActive = true });

        //Act
        var plan = await CreatePart().PlanAsync(CreateContext(), new TaxStoreSetupSection { Provider = "" });

        //Assert
        var change = Assert.Single(plan.Changes);
        Assert.Equal(($"providers.{FixedRateCode}.active", (object)true, (object)false), (change.Field, change.From, change.To));
    }

    [Theory]
    [InlineData("unknown provider", "'Unknown' is not installed")]
    [InlineData("settings without a provider", "The settings need a provider")]
    [InlineData("secret setting", "is a secret")]
    public async Task PlanAsync_RefusesWhatTheProvidersCannotTake(string gap, string expectedError)
    {
        //Arrange
        var section = gap switch
        {
            "unknown provider" => new TaxStoreSetupSection { Provider = "Unknown" },
            "settings without a provider" => new TaxStoreSetupSection { Settings = new Dictionary<string, object> { [_rate.Name] = 0.19m } },
            _ => new TaxStoreSetupSection { Provider = TestCode, Settings = new Dictionary<string, object> { [_apiKey.Name] = "avalara-key" } },
        };

        //Act
        var plan = await CreatePart().PlanAsync(CreateContext(), section);

        //Assert
        Assert.Contains(plan.Errors, x => x.Contains(expectedError));
        Assert.DoesNotContain(plan.Errors, x => x.Contains("avalara-key"));
    }

    [Fact]
    public async Task ApplyAsync_SavesTheChangedProvidersForTheStore_AndARepeatChangesNothing()
    {
        //Arrange
        var part = CreatePart();
        var section = new TaxStoreSetupSection { Provider = TestCode, Settings = new Dictionary<string, object> { [_rate.Name] = 0.19m } };
        var context = CreateContext();
        var plan = await part.PlanAsync(context, section);

        //Act
        await part.ApplyAsync(context, plan);
        var repeat = await part.PlanAsync(CreateContext(), section);

        //Assert
        var saved = Assert.Single(_saved);
        Assert.Equal((TestCode, StoreId, true), (saved.Code, saved.StoreId, saved.IsActive));
        Assert.Equal(0.19m, saved.Settings.Single(x => x.Name == _rate.Name).Value);
        Assert.Empty(repeat.Changes);
    }

    private static StoreSetupContext CreateContext()
    {
        return new StoreSetupContext { Store = new Store { Id = StoreId, Name = "EU store", Settings = [] } };
    }

    private TaxStoreSetupPart CreatePart()
    {
        var searchService = new Mock<ITaxProviderSearchService>();
        searchService
            .Setup(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((TaxProviderSearchCriteria criteria, bool _) =>
            {
                var saved = _saved.Where(x => criteria.StoreIds.Contains(x.StoreId)).Select(x => x.CloneTyped()).ToList();
                var installed = new TaxProvider[] { new FixedRateTaxProvider(), new TestTaxProvider() };
                var all = saved.Concat(installed.Where(x => !saved.Any(s => s.Code == x.Code))).ToList();

                return new TaxProviderSearchResult { TotalCount = all.Count, Results = all.Skip(criteria.Skip).Take(criteria.Take).ToList() };
            });

        _providerService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<TaxProvider>>()))
            .Callback((IList<TaxProvider> providers) =>
            {
                foreach (var provider in providers)
                {
                    _saved.RemoveAll(x => x.Code == provider.Code && x.StoreId == provider.StoreId);
                    _saved.Add(provider.CloneTyped());
                }
            })
            .Returns(Task.CompletedTask);

        var settingsRegistrar = new Mock<ISettingsRegistrar>();
        settingsRegistrar.Setup(x => x.GetSettingsForType(nameof(TestTaxProvider))).Returns([_rate, _apiKey]);
        settingsRegistrar.Setup(x => x.GetSettingsForType(nameof(FixedRateTaxProvider))).Returns([]);

        return new TestableTaxStoreSetupPart(searchService.Object, _providerService.Object, settingsRegistrar.Object, new TestStoreSetupSettings());
    }

    // The part with a fixed list of installed providers: the factory it reads them from is global, and other tests
    // register their own provider types there.
    private sealed class TestableTaxStoreSetupPart(
        ITaxProviderSearchService taxProviderSearchService,
        ITaxProviderService taxProviderService,
        ISettingsRegistrar settingsRegistrar,
        IStoreSetupSettings setupSettings)
        : TaxStoreSetupPart(taxProviderSearchService, taxProviderService, settingsRegistrar, setupSettings)
    {
        protected override IEnumerable<TaxProvider> GetInstalledProviders()
        {
            return [new FixedRateTaxProvider(), new TestTaxProvider()];
        }
    }
}
