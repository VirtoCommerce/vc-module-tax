using VirtoCommerce.TaxModule.Data.Provider;

namespace VirtoCommerce.TaxModule.Data.Tests.MCP.StoreSetup;

// A second installed tax provider, with settings of its own.
public class TestTaxProvider : FixedRateTaxProvider
{
    public TestTaxProvider()
    {
        Code = "TestTax";
    }
}
