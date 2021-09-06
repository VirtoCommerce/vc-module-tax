using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.TaxModule.Data.Model;
using VirtoCommerce.TaxModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.TaxModule.Core.Events;

namespace VirtoCommerce.TaxModule.Data.Services
{
    public class TaxProviderService : CrudService<TaxProvider, StoreTaxProviderEntity, TaxChangeEvent, TaxChangedEvent>, ITaxProviderService, ITaxProviderRegistrar
    {
        private readonly ISettingsManager _settingManager;

        public TaxProviderService(Func<ITaxRepository> repositoryFactory, IPlatformMemoryCache platformMemoryCache, IEventPublisher eventPublisher, ISettingsManager settingManager)
            : base(repositoryFactory, platformMemoryCache, eventPublisher)
        {
            _settingManager = settingManager;
        }

        public void RegisterTaxProvider<T>(Func<T> factory = null) where T : TaxProvider
        {
            if (AbstractTypeFactory<TaxProvider>.AllTypeInfos.All(t => t.Type != typeof(T)))
            {
                var typeInfo = AbstractTypeFactory<TaxProvider>.RegisterType<T>();
                if (factory != null)
                {
                    typeInfo.WithFactory(factory);
                }
            }
        }

        protected override TaxProvider ProcessModel(string responseGroup, StoreTaxProviderEntity entity, TaxProvider model)
        {
            var taxProvider = AbstractTypeFactory<TaxProvider>.TryCreateInstance(string.IsNullOrEmpty(entity.TypeName) ? $"{entity.Code}TaxProvider" : entity.TypeName);
            if (taxProvider != null)
            {
                entity.ToModel(taxProvider);

                _settingManager.DeepLoadSettingsAsync(taxProvider);
                return taxProvider;
            }
            return null;
        }

        protected override Task AfterSaveChangesAsync(IEnumerable<TaxProvider> models, IEnumerable<GenericChangedEntry<TaxProvider>> changedEntries)
        {
            return _settingManager.DeepSaveSettingsAsync(models);
        }

        protected override Task AfterDeleteAsync(IEnumerable<TaxProvider> models, IEnumerable<GenericChangedEntry<TaxProvider>> changedEntries)
        {
            return _settingManager.DeepRemoveSettingsAsync(models);
        }

        protected override Task<IEnumerable<StoreTaxProviderEntity>> LoadEntities(IRepository repository, IEnumerable<string> ids, string responseGroup)
        {
            return ((ITaxRepository)repository).GetByIdsAsync(ids);
        }

        public async Task<TaxProvider[]> GetByIdsAsync(string[] ids, string responseGroup)
        {
            var result = await base.GetByIdsAsync(ids);
            return result.ToArray();
        }

        public Task SaveChangesAsync(TaxProvider[] taxProviders)
        {
            return base.SaveChangesAsync(taxProviders);
        }

        public Task DeleteAsync(string[] ids)
        {
            return base.DeleteAsync(ids);
        }
    }
}
