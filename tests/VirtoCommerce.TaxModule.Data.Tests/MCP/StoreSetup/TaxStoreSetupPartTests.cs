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
    private static readonly SettingDescriptor _accountId = new() { Name = "Test.Tax.AccountId", ValueType = SettingValueType.ShortText, IsRequired = true };

    // The providers the store has saved; the search adds a new instance of every installed provider the store has not saved.
    private readonly List<TaxProvider> _saved = [];
    private readonly Mock<ITaxProviderService> _providerService = new();
    // The settings manager's cached entries: every read of an unsaved test method gets a new list of these instances.
    private readonly List<ObjectSettingEntry> _cachedSettings = [];

    [Fact]
    public async Task GetChoicesAsync_ListsTheInstalledProviders_WithTheirSettingsThatAreNotSecrets()
    {
        //Act
        var choices = await CreatePart().GetChoicesAsync();

        //Assert
        Assert.Equal([FixedRateCode, TestCode], choices.Select(x => x.Value));
        Assert.All(choices, x => Assert.Equal("tax.provider", x.Field));
        Assert.Equal([_rate.Name, _accountId.Name], choices.Single(x => x.Value == TestCode).Settings.Select(x => x.Name));
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
        _saved.Add(new TestTaxProvider { StoreId = StoreId, IsActive = true, Priority = 1, Settings = [new ObjectSettingEntry(_accountId) { Value = "ACC-1" }] });
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

    // Applying reads the providers again: a setting changed in the back office since the plan, which the plan does not
    // name, keeps its new value.
    [Fact]
    public async Task ApplyAsync_KeepsWhatChangedSinceThePlan()
    {
        //Arrange
        _saved.Add(new TestTaxProvider { StoreId = StoreId, IsActive = true, Settings = [new ObjectSettingEntry(_rate) { Value = 0.07m }] });
        var part = CreatePart();
        var context = CreateContext();
        var plan = await part.PlanAsync(context, new TaxStoreSetupSection { Provider = TestCode, Settings = new Dictionary<string, object> { [_rate.Name] = 0.19m } });
        _saved.Single().Settings.Add(new ObjectSettingEntry(_accountId) { Value = "ACC-1" });

        //Act
        await part.ApplyAsync(context, plan);

        //Assert
        var saved = Assert.Single(_saved);
        Assert.Equal(0.19m, saved.Settings.Single(x => x.Name == _rate.Name).Value);
        Assert.Equal("ACC-1", saved.Settings.Single(x => x.Name == _accountId.Name).Value);
    }

    // Q6: a required setting left empty blocks going live, as for payment and shipping methods.
    [Fact]
    public async Task CheckAsync_ActiveProviderWithAnEmptyRequiredSetting_IsBlocking()
    {
        //Arrange
        _saved.Add(new TestTaxProvider { StoreId = StoreId, IsActive = true });

        //Act
        var findings = await CreatePart().CheckAsync(CreateContext());

        //Assert
        var finding = Assert.Single(findings, x => x.Id == "tax-provider-needs-settings");
        Assert.Equal(McpFindingSeverities.Critical, finding.Severity);
        Assert.Equal("Empty: Test.Tax.AccountId.", finding.Evidence);
        Assert.False(finding.ManualOnly);
        Assert.Equal(TestCode, finding.ObjectId);
    }

    private static StoreSetupContext CreateContext()
    {
        return new StoreSetupContext { Store = new Store { Id = StoreId, Name = "EU store", Settings = [] } };
    }

    // A dry run, the plan made again at submit, and the apply never write to the settings manager's cached entries.
    [Fact]
    public async Task PlanAndApply_NeverWriteToTheSettingsManagersCachedEntries()
    {
        //Arrange
        var cached = new ObjectSettingEntry(_rate) { Value = 0m };
        _cachedSettings.Add(cached);
        var section = new TaxStoreSetupSection { Provider = TestCode, Settings = new Dictionary<string, object> { [_rate.Name] = 9.5m } };

        //Act
        var dryRun = await CreatePart().PlanAsync(CreateContext(), section);
        var atSubmit = await CreatePart().PlanAsync(CreateContext(), section);
        await CreatePart().ApplyAsync(CreateContext(), atSubmit);

        //Assert
        Assert.Equal(0m, cached.Value);
        Assert.Contains(dryRun.Changes, x => x.Field == $"providers.{TestCode}.{_rate.Name}");
        Assert.Contains(atSubmit.Changes, x => x.Field == $"providers.{TestCode}.{_rate.Name}");
        var savedProviders = (IList<TaxProvider>)_providerService.Invocations.Last(x => x.Method.Name == nameof(ITaxProviderService.SaveChangesAsync)).Arguments[0];
        Assert.DoesNotContain(savedProviders.SelectMany(x => x.Settings), x => ReferenceEquals(x, cached));
        Assert.Equal(9.5m, savedProviders.Single(x => x.Code == TestCode).Settings.Single(x => x.Name == _rate.Name).Value);
    }

    private TaxStoreSetupPart CreatePart()
    {
        var searchService = new Mock<ITaxProviderSearchService>();
        searchService
            .Setup(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((TaxProviderSearchCriteria criteria, bool clone) =>
            {
                // As the search service: copies when asked, the saved instances themselves otherwise.
                var saved = _saved.Where(x => criteria.StoreIds.Contains(x.StoreId)).Select(x => clone ? x.CloneTyped() : x).ToList();
                var installed = new TaxProvider[] { new FixedRateTaxProvider(), new TestTaxProvider { Settings = _cachedSettings.ToList() } };
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
        settingsRegistrar.Setup(x => x.GetSettingsForType(nameof(TestTaxProvider))).Returns([_rate, _apiKey, _accountId]);
        settingsRegistrar.Setup(x => x.GetSettingsForType(nameof(FixedRateTaxProvider))).Returns([]);

        return new TestableTaxStoreSetupPart(searchService.Object, _providerService.Object, settingsRegistrar.Object, new TestStoreSetupSettings());
    }
}
