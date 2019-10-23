using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Swagger;

namespace VirtoCommerce.TaxModule.Core.Model
{
    [SwaggerSchemaId("TaxStore")]
    public class Store : Entity, IHasOuterId
    {
        public string Name { get; set; }
        public string OuterId { get; set; }

        public string TimeZone { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public string DefaultLanguage { get; set; }

        public string DefaultCurrency { get; set; }
    }
}
