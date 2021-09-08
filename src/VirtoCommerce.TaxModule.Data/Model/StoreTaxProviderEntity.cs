using System;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.TaxModule.Core.Model;

namespace VirtoCommerce.TaxModule.Data.Model
{
    public class StoreTaxProviderEntity : Entity, IDataEntity<StoreTaxProviderEntity, TaxProvider>
    {
        [Required]
        [StringLength(128)]
        public string Code { get; set; }

        public int Priority { get; set; }

        [Required]
        [StringLength(128)]
        public string TypeName { get; set; }

        [StringLength(2048)]
        public string LogoUrl { get; set; }

        public bool IsActive { get; set; }

        #region Navigation Properties

        public string StoreId { get; set; }

        #endregion

        public virtual TaxProvider ToModel(TaxProvider model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            model.Id = Id;
            model.IsActive = IsActive;
            model.Code = Code;
            model.LogoUrl = LogoUrl;
            model.Priority = Priority;
            model.StoreId = StoreId;
            return model;
        }

        public virtual StoreTaxProviderEntity FromModel(TaxProvider model, PrimaryKeyResolvingMap pkMap)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            pkMap.AddPair(model, this);
            Id = model.Id;
            IsActive = model.IsActive;
            Code = model.Code;
            LogoUrl = model.LogoUrl;
            Priority = model.Priority;
            StoreId = model.StoreId;
            TypeName = model.TypeName;
            return this;
        }

        public virtual void Patch(StoreTaxProviderEntity target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.IsActive = IsActive;
            target.LogoUrl = LogoUrl;
            target.Priority = Priority;
        }
    }
}
