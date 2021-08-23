using System.Collections.Generic;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.TaxModule.Core.Events
{
    public class TaxChangedEvent : GenericChangedEntryEvent<TaxProvider>
    {
        public TaxChangedEvent(IEnumerable<GenericChangedEntry<TaxProvider>> changedEntries) : base(changedEntries)
        {
        }
    }
}
