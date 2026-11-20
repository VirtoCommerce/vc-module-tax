using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.MCP.Core.Models;
using VirtoCommerce.Platform.Core.Common;
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
    public virtual McpSetupPermissions Permissions => new()
    {
        Read = TaxPermissions.Read,
        Update = TaxPermissions.Update,
        Create = TaxPermissions.Update,
    };

    public virtual Type SectionType => typeof(TaxStoreSetupSection);

    public virtual bool IsRequiredForNewStore => false;

    // 1. What can be chosen: every tax provider installed on the platform, with the settings it takes.
    public virtual Task<IList<McpSetupChoice>> GetChoicesAsync()
    {
        IList<McpSetupChoice> choices = GetInstalledProviders()
            .Select(provider =>
            {
                var choice = AbstractTypeFactory<McpSetupChoice>.TryCreateInstance();
                choice.Field = $"{PartName}.provider";
                choice.Value = provider.Code;
                choice.Title = provider.Code;

                // Secrets are not even listed, so the agent never proposes to send one.
                choice.Settings = setupSettings.DescribeAll(GetSettingDescriptors(provider));

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
            : setupSettings.GetValues(provider, GetSettingDescriptors(provider));

        return section;
    }

    // 3. What is missing. With a tax section in the request, the check reads the providers as they will be after the
    // change; otherwise the saved ones. Both rules are warnings: a store without tax can still take orders.
    public virtual async Task<IList<McpFinding>> CheckAsync(StoreSetupContext context)
    {
        var store = context.Store;
        var providers = context.Plans.TryGetValue(PartName, out var plan) && plan is TaxStoreSetupPlan taxPlan
            ? taxPlan.Providers
            : await GetStoreProvidersAsync(store.Id);

        // In the search's order, the order in which the cart reads them.
        var activeProviders = providers.Where(x => x.IsActive).ToList();
        var findings = new List<McpFinding>();

        if (activeProviders.Count == 0 && IsTaxCalculationEnabled(store))
        {
            findings.Add(CreateFinding(store, "tax-no-active-provider", McpFindingSeverities.Warning,
                "Tax calculation is on, but no tax provider is active",
                "Prices are shown and orders are placed without tax.",
                "Tax calculation: on. Active tax providers: none.",
                "Activate a tax provider, or switch tax calculation off in the store settings."));
        }

        if (activeProviders.Count > 1)
        {
            findings.Add(CreateFinding(store, "tax-several-active-providers", McpFindingSeverities.Warning,
                "Several tax providers are active",
                "The cart uses only the first active provider; the others have no effect.",
                $"Active, in order: {string.Join(", ", activeProviders.Select(x => x.Code))}.",
                "Keep one tax provider active."));
        }

        foreach (var provider in activeProviders)
        {
            // A secret is never named, not even when it is missing: a person enters it in the back office.
            var (names, secretCount) = setupSettings.GetEmptyRequiredSettings(provider, GetSettingDescriptors(provider));

            if (names.Count == 0 && secretCount == 0)
            {
                continue;
            }

            var finding = CreateFinding(store, "tax-provider-needs-settings", McpFindingSeverities.Critical,
                $"The tax provider {provider.Code} needs its required settings",
                "Taxes cannot be calculated at checkout until its required settings have values.",
                StoreSetupFindings.GetEmptySettingsEvidence(names, secretCount),
                secretCount == 0
                    ? "Fill the settings in the tax section, or in the back office."
                    : names.Count == 0
                        ? "A person enters the secret settings in the back office: secrets are never sent through the agent."
                        : "Fill the other settings in the tax section; a person enters the secret settings in the back office.");
            finding.ManualOnly = names.Count == 0;
            finding.ObjectType = nameof(TaxProvider);
            finding.ObjectId = provider.Code;
            finding.ObjectName = provider.Code;

            findings.Add(finding);
        }

        return findings;
    }

    // 4a. Plans the section without side effects: the search returns copies, and nothing is saved here. A provider code
    // activates that provider and switches the others off; an empty code switches every provider off; without a code the
    // providers stay as they are, and the settings go to the provider the cart uses.
    public virtual async Task<McpSetupPartPlan> PlanAsync(StoreSetupContext context, object section)
    {
        var tax = (TaxStoreSetupSection)section;

        var plan = AbstractTypeFactory<TaxStoreSetupPlan>.TryCreateInstance();
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
    public virtual async Task ApplyAsync(StoreSetupContext context, McpSetupPartPlan plan)
    {
        var taxPlan = (TaxStoreSetupPlan)plan;
        var providers = await GetStoreProvidersAsync(context.Store.Id);
        var changed = new List<TaxProvider>();

        // The providers read again: the fields of the plan's rows take their planned values, so a change made since the
        // plan stays.
        foreach (var planned in taxPlan.Providers)
        {
            var fields = taxPlan.GetChangedFields($"providers.{planned.Code}.");

            if (fields.Count == 0)
            {
                continue;
            }

            var provider = providers.First(x => x.Code.EqualsIgnoreCase(planned.Code));
            ApplyChangedFields(planned, provider, fields);
            // A provider the store has never saved comes from the search without a store.
            provider.StoreId = context.Store.Id;
            changed.Add(provider);
        }

        if (changed.Count > 0)
        {
            await taxProviderService.SaveChangesAsync(changed);
        }
    }

    // The planned values of the fields the plan changes, on the provider read again.
    protected virtual void ApplyChangedFields(TaxProvider planned, TaxProvider provider, ISet<string> fields)
    {
        if (fields.Contains("active"))
        {
            provider.IsActive = planned.IsActive;
        }

        setupSettings.CopySettings(planned, provider, fields);
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
        // Store's rules for a settings map, the same in every part: an unknown name, a secret or a wrong value is
        // refused, a null returns the default, and a value equal to the one in effect changes nothing.
        setupSettings.PlanSettings(plan, provider, GetSettingDescriptors(provider), settings,
            $"providers.{provider.Code}.", $"Tax provider '{provider.Code}'", "get_store_setup_options");
    }

    // The providers installed on the platform: the types registered with the factory, as Tax's own search reads them.
    protected virtual IEnumerable<TaxProvider> GetInstalledProviders()
    {
        return AbstractTypeFactory<TaxProvider>.AllTypeInfos.Select(x => AbstractTypeFactory<TaxProvider>.TryCreateInstance(x.Type.Name));
    }

    // The store's saved providers, plus a new instance of every installed provider the store has not saved yet,
    // each a copy: the settings of a provider the store has not saved are the settings manager's cached entries, which
    // every unsaved provider of the type shares in every store, and planning, applying and saving write to the entries.
    protected virtual async Task<IList<TaxProvider>> GetStoreProvidersAsync(string storeId)
    {
        var criteria = AbstractTypeFactory<TaxProviderSearchCriteria>.TryCreateInstance();
        criteria.StoreIds = [storeId];

        return (await taxProviderSearchService.SearchAllAsync(criteria)).Select(x => x.CloneTyped()).ToList();
    }

    // The settings a provider takes: each tax module registers them for its provider's type name.
    protected virtual IList<SettingDescriptor> GetSettingDescriptors(TaxProvider provider)
    {
        return settingsRegistrar.GetSettingsForType(provider.TypeName)?.ToList() ?? [];
    }

    // Tax calculation as the store sets it, or as it would be set after the settings part of the same request.
    protected virtual bool IsTaxCalculationEnabled(Store store)
    {
        return (store.Settings ?? []).GetValue<bool>(StoreGeneralSettings.TaxCalculationEnabled);
    }

    protected virtual McpFinding CreateFinding(Store store, string id, string severity, string title, string why, string evidence, string fix)
    {
        var finding = StoreSetupFindings.ForStore(store, PartName, id, severity, "Tax providers");
        finding.Title = title;
        finding.Why = why;
        finding.Evidence = evidence;
        finding.Fix = fix;

        return finding;
    }

    // The provider the cart uses: the first active one in the search's order, as the cart and Tax's own REST API take it.
    private static TaxProvider GetUsedProvider(IList<TaxProvider> providers)
    {
        return providers.FirstOrDefault(x => x.IsActive);
    }

    private static void AddChange(TaxStoreSetupPlan plan, TaxProvider provider, string field, object from, object to)
    {
        plan.AddChange($"providers.{provider.Code}.{field}", from, to);
    }
}
