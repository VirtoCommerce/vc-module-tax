using System.Collections.Generic;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.TaxModule.Core.Events
{
    public class TaxChangeEvent : GenericChangedEntryEvent<TaxProvider>
    {
        public TaxChangeEvent(IEnumerable<GenericChangedEntry<TaxProvider>> changedEntries) : base(changedEntries)
        {
        }
    }
}
