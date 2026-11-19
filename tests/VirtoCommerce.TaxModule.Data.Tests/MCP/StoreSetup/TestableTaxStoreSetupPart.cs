using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.TaxModule.Data.MCP.StoreSetup;
using VirtoCommerce.TaxModule.Data.Provider;

namespace VirtoCommerce.TaxModule.Data.Tests.MCP.StoreSetup;

// The part with a fixed list of installed providers: the factory it reads them from is global, and other tests register
// their own provider types there.
public sealed class TestableTaxStoreSetupPart(
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
