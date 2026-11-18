using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Extensions;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Core.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;
using VirtoCommerce.TaxModule.Core.Services;
using Store = VirtoCommerce.StoreModule.Core.Model.Store;
using StoreGeneralSettings = VirtoCommerce.StoreModule.Core.ModuleConstants.Settings.General;
using TaxPermissions = VirtoCommerce.TaxModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.TaxModule.Data.MCP.StoreSetup;

// Tax's part of a store's setup: the one tax provider a store uses, with its settings that are not secrets. The cart uses
// only the first active provider, so the section names one provider and switches the others off.
// Store never references this class: Tax registers it in its Module.Initialize with AddStoreSetupPart.
public class TaxStoreSetupPart(
    ITaxProviderSearchService taxProviderSearchService,
    ITaxProviderService taxProviderService,
    ISettingsRegistrar settingsRegistrar,
    IStoreSetupSettings setupSettings)
    : IStoreSetupChoices, IStoreSetupState, IStoreSetupCheck, IStoreSetupSection
{
    public const string PartName = "tax";

    public virtual string Name => PartName;

    public virtual string Title => "Tax provider";

    // Tax's permissions cannot be limited to stores, so they count only when granted globally.
    public virtual StoreSetupPermissions Permissions => new()
    {
        Read = TaxPermissions.Read,
        Update = TaxPermissions.Update,
        Create = TaxPermissions.Update,
    };

    public virtual Type SectionType => typeof(TaxStoreSetupSection);

    public virtual bool IsRequiredForNewStore => false;

    // 1. What can be chosen: every tax provider installed on the platform, with the settings it takes.
    public virtual Task<IList<StoreSetupChoice>> GetChoicesAsync()
    {
        IList<StoreSetupChoice> choices = GetInstalledProviders()
            .Select(provider =>
            {
                var choice = AbstractTypeFactory<StoreSetupChoice>.TryCreateInstance();
                choice.Field = $"{PartName}.provider";
                choice.Value = provider.Code;
                choice.Title = provider.Code;

                // Secrets are not even listed, so the agent never proposes to send one.
                choice.Settings = GetSettingDescriptors(provider)
                    .Where(x => !setupSettings.IsSecret(x))
                    .Select(x => setupSettings.Describe(x, value: null))
                    .ToList();

                return choice;
            })
            .ToList();

        return Task.FromResult(choices);
    }

    // 2. The store's current state, in the shape of the section: the provider the cart uses, with its settings.
    public virtual async Task<object> GetStateAsync(Store store)
    {
        var provider = GetUsedProvider(await GetStoreProvidersAsync(store.Id));

        var section = AbstractTypeFactory<TaxStoreSetupSection>.TryCreateInstance();
        section.Provider = provider?.Code ?? string.Empty;
        section.Settings = provider == null
            ? new Dictionary<string, object>()
            : GetSettingDescriptors(provider)
                .Where(x => !setupSettings.IsSecret(x))
                .ToDictionary(x => x.Name, x => GetValue(provider, x), StringComparer.OrdinalIgnoreCase);

        return section;
    }

    // 3. What is missing. With a tax section in the request, the check reads the providers as they will be after the
    // change; otherwise the saved ones. Both rules are warnings: a store without tax can still take orders.
    public virtual async Task<IList<StoreSetupFinding>> CheckAsync(StoreSetupContext context)
    {
        var store = context.Store;
        var providers = context.Plans.TryGetValue(PartName, out var plan) && plan is TaxStoreSetupPlan taxPlan
            ? taxPlan.Providers
            : await GetStoreProvidersAsync(store.Id);

        var activeProviders = providers.Where(x => x.IsActive).OrderBy(x => x.Priority).ToList();
        var findings = new List<StoreSetupFinding>();

        if (activeProviders.Count == 0 && IsTaxCalculationEnabled(store))
        {
            findings.Add(CreateFinding(store, "tax-no-active-provider",
                "Tax calculation is on, but no tax provider is active",
                "Prices are shown and orders are placed without tax.",
                "Tax calculation: on. Active tax providers: none.",
                "Activate a tax provider, or switch tax calculation off in the store settings."));
        }

        if (activeProviders.Count > 1)
        {
            findings.Add(CreateFinding(store, "tax-several-active-providers",
                "Several tax providers are active",
                "The cart uses only the first active provider; the others have no effect.",
                $"Active, in order: {string.Join(", ", activeProviders.Select(x => x.Code))}.",
                "Keep one tax provider active."));
        }

        return findings;
    }

    // 4a. Plans the section without side effects: the search returns copies, and nothing is saved here. A provider code
    // activates that provider and switches the others off; an empty code switches every provider off; without a code the
    // providers stay as they are, and the settings go to the provider the cart uses.
    public virtual async Task<StoreSetupPartPlan> PlanAsync(StoreSetupContext context, object section)
    {
        var tax = (TaxStoreSetupSection)section;

        var plan = AbstractTypeFactory<TaxStoreSetupPlan>.TryCreateInstance();
        plan.Part = PartName;
        plan.Providers = await GetStoreProvidersAsync(context.Store.Id);

        TaxProvider target;

        if (tax.Provider == null)
        {
            target = GetUsedProvider(plan.Providers);
        }
        else
        {
            var code = tax.Provider.Trim();
            target = code.Length == 0 ? null : plan.Providers.FirstOrDefault(x => x.Code.EqualsIgnoreCase(code));

            if (code.Length > 0 && target == null)
            {
                plan.Errors.Add($"Tax provider '{code}' is not installed. get_store_setup_options lists the codes.");
                return plan;
            }

            foreach (var provider in plan.Providers)
            {
                PlanActive(plan, provider, isActive: provider == target);
            }
        }

        if (!tax.Settings.IsNullOrEmpty())
        {
            if (target == null)
            {
                plan.Errors.Add("The settings need a provider: name it in provider.");
            }
            else
            {
                PlanSettings(plan, target, tax.Settings);
            }
        }

        return plan;
    }

    // 4b. Saves the providers the plan changes. The plan was made from the saved rows a moment ago, so a repeat after a
    // failure finds the applied providers unchanged and saves only the rest.
    public virtual async Task ApplyAsync(StoreSetupContext context, StoreSetupPartPlan plan)
    {
        var taxPlan = (TaxStoreSetupPlan)plan;
        var changed = taxPlan.Providers.Where(x => taxPlan.ChangedCodes.Contains(x.Code)).ToList();

        if (changed.Count == 0)
        {
            return;
        }

        // A provider the store has never saved comes from the search without a store.
        foreach (var provider in changed)
        {
            provider.StoreId = context.Store.Id;
        }

        await taxProviderService.SaveChangesAsync(changed);
    }

    protected virtual void PlanActive(TaxStoreSetupPlan plan, TaxProvider provider, bool isActive)
    {
        if (provider.IsActive == isActive)
        {
            return;
        }

        AddChange(plan, provider, "active", provider.IsActive, isActive);

        if (!isActive)
        {
            plan.Warnings.Add($"Switches off the tax provider {provider.Code}.");
        }

        provider.IsActive = isActive;
    }

    protected virtual void PlanSettings(TaxStoreSetupPlan plan, TaxProvider provider, IDictionary<string, object> settings)
    {
        var descriptors = GetSettingDescriptors(provider);

        // A provider the store has never saved starts with an empty array.
        provider.Settings = provider.Settings?.ToList() ?? [];

        foreach (var (name, raw) in settings)
        {
            var descriptor = descriptors.FirstOrDefault(x => x.Name.EqualsIgnoreCase(name));

            if (descriptor == null)
            {
                plan.Errors.Add($"Tax provider '{provider.Code}' has no setting '{name}'.");
                continue;
            }

            // Store's one refusal for every part: a secret, a setting a setup cannot change, a wrong value.
            if (!setupSettings.TryConvert(descriptor, raw, out var value, out var error))
            {
                plan.Errors.Add(error);
                continue;
            }

            var entry = provider.Settings.FirstOrDefault(x => x.Name.EqualsIgnoreCase(name));

            if (entry == null)
            {
                entry = new ObjectSettingEntry(descriptor);
                provider.Settings.Add(entry);
            }

            // A null saves the default itself: the platform's settings save skips a null for most settings.
            var newValue = value ?? descriptor.DefaultValue;

            if (!StoreSetupValues.AreEqual(entry.Value, newValue))
            {
                AddChange(plan, provider, name, entry.Value, newValue);
                entry.Value = newValue;
            }
        }
    }

    // The providers installed on the platform: the types registered with the factory, as Tax's own search reads them.
    protected virtual IEnumerable<TaxProvider> GetInstalledProviders()
    {
        return AbstractTypeFactory<TaxProvider>.AllTypeInfos.Select(x => AbstractTypeFactory<TaxProvider>.TryCreateInstance(x.Type.Name));
    }

    // The store's saved providers, plus a new instance of every installed provider the store has not saved yet.
    protected virtual async Task<IList<TaxProvider>> GetStoreProvidersAsync(string storeId)
    {
        var criteria = AbstractTypeFactory<TaxProviderSearchCriteria>.TryCreateInstance();
        criteria.StoreIds = [storeId];

        return await taxProviderSearchService.SearchAllAsync(criteria);
    }

    // The settings a provider takes: each tax module registers them for its provider's type name.
    protected virtual IList<SettingDescriptor> GetSettingDescriptors(TaxProvider provider)
    {
        return settingsRegistrar.GetSettingsForType(provider.TypeName)?.ToList() ?? [];
    }

    // Tax calculation as the store sets it, or as it would be set after the settings part of the same request.
    protected virtual bool IsTaxCalculationEnabled(Store store)
    {
        var setting = StoreGeneralSettings.TaxCalculationEnabled;
        var value = store.Settings?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(setting.Name))?.Value ?? setting.DefaultValue;

        return value is bool enabled ? enabled : bool.TryParse(value?.ToString(), out var parsed) && parsed;
    }

    protected virtual StoreSetupFinding CreateFinding(Store store, string id, string title, string why, string evidence, string fix)
    {
        var finding = AbstractTypeFactory<StoreSetupFinding>.TryCreateInstance();
        finding.Id = id;
        finding.Part = PartName;
        finding.Severity = StoreSetupFindingSeverity.Warning;
        finding.Title = title;
        finding.Why = why;
        finding.Evidence = evidence;
        finding.Fix = fix;
        finding.Screen = $"Stores > {store.Name ?? store.Id} > Tax providers";
        finding.ObjectType = nameof(Store);
        finding.ObjectId = store.Id;
        finding.ObjectName = store.Name;

        return finding;
    }

    // The provider the cart uses: the first active one by priority.
    private static TaxProvider GetUsedProvider(IList<TaxProvider> providers)
    {
        return providers.Where(x => x.IsActive).OrderBy(x => x.Priority).FirstOrDefault();
    }

    private static void AddChange(TaxStoreSetupPlan plan, TaxProvider provider, string field, object from, object to)
    {
        plan.AddChange($"providers.{provider.Code}.{field}", from, to);
        plan.ChangedCodes.Add(provider.Code);
    }

    private static object GetValue(TaxProvider provider, SettingDescriptor descriptor)
    {
        return provider.Settings?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(descriptor.Name))?.Value ?? descriptor.DefaultValue;
    }
}
