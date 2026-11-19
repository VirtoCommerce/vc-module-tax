using System;
using System.Collections.Generic;
using VirtoCommerce.MCP.Core.Models;
using VirtoCommerce.TaxModule.Core.Model;

namespace VirtoCommerce.TaxModule.Data.MCP.StoreSetup;

// The plan of the tax section, carrying the store's providers as they will be after the change: the check reads them for
// the preview ("what the store would miss afterwards"), and applying saves the changed ones.
public class TaxStoreSetupPlan : McpSetupPartPlan
{
    public IList<TaxProvider> Providers { get; set; } = [];

    public ISet<string> ChangedCodes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
