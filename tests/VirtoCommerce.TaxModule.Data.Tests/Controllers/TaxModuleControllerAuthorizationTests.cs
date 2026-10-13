using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.TaxModule.Core;
using VirtoCommerce.TaxModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.TaxModule.Data.Tests.Controllers
{
    public class TaxModuleControllerAuthorizationTests
    {
        [Fact]
        public void Controller_Requires_Authenticated_User()
        {
            Assert.NotNull(typeof(TaxModuleController).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Theory]
        [InlineData(nameof(TaxModuleController.SearchTaxProviders), ModuleConstants.Security.Permissions.Read)]
        [InlineData(nameof(TaxModuleController.GetTaxProviderById), ModuleConstants.Security.Permissions.Read)]
        [InlineData(nameof(TaxModuleController.EvaluateTaxes), ModuleConstants.Security.Permissions.Read)]
        [InlineData(nameof(TaxModuleController.UpdateTaxProvider), ModuleConstants.Security.Permissions.Update)]
        public void Action_Requires_Permission(string actionName, string permission)
        {
            var policies = typeof(TaxModuleController).GetMethod(actionName)
                .GetCustomAttributes<AuthorizeAttribute>()
                .Select(x => x.Policy);

            Assert.Contains(permission, policies);
        }

        [Fact]
        public void Module_Registers_Tax_Permissions()
        {
            Assert.Equal(["tax:read", "tax:update"], ModuleConstants.Security.Permissions.AllPermissions);
        }

        [Fact]
        public void Every_Action_Requires_A_Permission()
        {
            var actions = typeof(TaxModuleController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.NotEmpty(actions);
            Assert.All(actions, action =>
            {
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
                Assert.Contains(action.GetCustomAttributes<AuthorizeAttribute>(), x => !string.IsNullOrEmpty(x.Policy));
            });
        }
    }
}
