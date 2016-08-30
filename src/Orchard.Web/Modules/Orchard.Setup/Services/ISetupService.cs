using System.Collections.Generic;
using System.Threading.Tasks;
using Orchard.DependencyInjection;
using Orchard.Recipes.Models;

namespace Orchard.Setup.Services
{
    public interface ISetupService : IDependency
    {
        Task<IEnumerable<RecipeDescriptor>> GetSetupRecipesAsync();
        Task<string> SetupAsync(SetupContext context);
    }
}