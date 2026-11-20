using System.Collections.Generic;
using System.ComponentModel;

namespace VirtoCommerce.TaxModule.Core.MCP.StoreSetup;

// Tax's section of a store's setup, as an agent fills it: "tax": { "provider": ..., "settings": { ... } }. The cart uses
// only the first active provider, so the section names one provider. The descriptions become the JSON schema that
// get_store_setup_options shows.
public class TaxStoreSetupSection
{
    [Description("Code of the tax provider the store uses, as get_store_setup_options lists it; the other providers are " +
        "switched off. An empty text switches every provider off; a text of spaces only is refused. Left out: unchanged.")]
    public string Provider { get; set; }

    [Description("Setting name to value for the provider: the one named in provider, or the active one when provider is " +
        "left out. Leaves the other settings as they are; null returns a setting to its default, or clears a setting that " +
        "has allowed values but no default, and a setting with neither takes a value instead. Secrets are entered in the " +
        "back office.")]
    public IDictionary<string, object> Settings { get; set; }
}
