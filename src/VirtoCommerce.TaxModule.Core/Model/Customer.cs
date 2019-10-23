using System;
using System.Collections.Generic;
using System.Text;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Swagger;

namespace VirtoCommerce.TaxModule.Core.Model
{
    [SwaggerSchemaId("TaxCustomer")]
    public class Customer : Entity, IHasOuterId
    {
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }

        public string OuterId { get; set; }

        public IList<Address> Addresses { get; set; }
        public IList<string> Phones { get; set; }
        public IList<string> Emails { get; set; }
        public IList<string> Groups { get; set; }


        public DateTime? BirthDate { get; set; }
        public string DefaultLanguage { get; set; }
        public string TimeZone { get; set; }
        public IList<string> Organizations { get; set; }

        public string TaxPayerId { get; set; }
    }
}
