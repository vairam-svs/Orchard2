using Microsoft.AspNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Orchard.Data.Migration;
using Orchard.DependencyInjection;
using Orchard.Environment.Extensions;
using Orchard.Environment.Recipes.Models;
using Orchard.Environment.Recipes.Services;
using Orchard.Environment.Shell;
using Orchard.Environment.Shell.Builders;
using Orchard.Environment.Shell.Descriptor;
using Orchard.Environment.Shell.Descriptor.Models;
using Orchard.Environment.Shell.Models;
using Orchard.Environment.Shell.State;
using Orchard.Hosting;
using Orchard.Hosting.ShellBuilders;
using System;
using System.Collections.Generic;
using System.Linq;
using YesSql.Core.Services;

namespace Orchard.Setup.Services
{
    public class SetupService : Component, ISetupService
    {
        private readonly ShellSettings _shellSettings;
        private readonly IOrchardHost _orchardHost;
        private readonly IShellContextFactory _shellContextFactory;
        private readonly IExtensionManager _extensionManager;
        private readonly IRunningShellRouterTable _runningShellRouterTable;
        private readonly IRecipeHarvester _recipeHarvester;
        private readonly IProcessingEngine _processingEngine;
        private readonly ILogger _logger;
        private IReadOnlyList<Recipe> _recipes;

        public SetupService(
            ShellSettings shellSettings,
            IOrchardHost orchardHost,
            IShellContextFactory shellContextFactory,
            IExtensionManager extensionManager,
            IRunningShellRouterTable runningShellRouterTable,
            IRecipeHarvester recipeHarvester,
            IProcessingEngine processingEngine,
            ILogger<SetupService> logger)
        {
            _shellSettings = shellSettings;
            _orchardHost = orchardHost;
            _shellContextFactory = shellContextFactory;
            _extensionManager = extensionManager;
            _runningShellRouterTable = runningShellRouterTable;
            _recipeHarvester = recipeHarvester;
            _processingEngine = processingEngine;
            _logger = logger;
        }

        public ShellSettings Prime()
        {
            return _shellSettings;
        }

        public IReadOnlyList<Recipe> Recipes()
        {
            if (_recipes == null)
            {
                _recipes = _recipeHarvester
                    .HarvestRecipesAsync()
                    .Result
                    .Where(recipe => recipe.IsSetupRecipe)
                    .ToList();
            }
            return _recipes;
        }

        public string Setup(SetupContext context)
        {
            var initialState = _shellSettings.State;
            try
            {
                return SetupInternal(context);
            }
            catch
            {
                _shellSettings.State = initialState;
                throw;
            }
        }

        public string SetupInternal(SetupContext context)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Running setup for tenant '{0}'.", _shellSettings.Name);
            }

            // Features to enable for Setup
            string[] hardcoded = {
                "Orchard.Logging.Console",
                "Orchard.Hosting",
                "Settings",
                "Orchard.Modules",
                "Orchard.Themes",
                "Orchard.Recipes"
                };

            context.EnabledFeatures = hardcoded.Union(context.EnabledFeatures ?? Enumerable.Empty<string>()).Distinct().ToList();

            // Set shell state to "Initializing" so that subsequent HTTP requests are responded to with "Service Unavailable" while Orchard is setting up.
            _shellSettings.State = TenantState.Initializing;

            var shellSettings = new ShellSettings(_shellSettings);

            if (string.IsNullOrEmpty(shellSettings.DatabaseProvider))
            {
                shellSettings.DatabaseProvider = context.DatabaseProvider;
                shellSettings.ConnectionString = context.DatabaseConnectionString;
                shellSettings.TablePrefix = context.DatabaseTablePrefix;
            }

            // TODO: Add Encryption Settings in

            var shellDescriptor = new ShellDescriptor
            {
                Features = context.EnabledFeatures.Select(name => new ShellFeature { Name = name }).ToList()
            };

            // Creating a standalone environment based on a "minimum shell descriptor".
            // In theory this environment can be used to resolve any normal components by interface, and those
            // components will exist entirely in isolation - no crossover between the safemode container currently in effect
            // It is used to initialize the database before the recipe is run.

            using (var environment = _shellContextFactory.CreateDescribedContext(shellSettings, shellDescriptor))
            {
                using (var scope = environment.CreateServiceScope())
                {
                    var store = scope.ServiceProvider.GetRequiredService<IStore>();
                    store.InitializeAsync();
                    
                    // Create the "minimum shell descriptor"
                    scope
                        .ServiceProvider
                        .GetRequiredService<IShellDescriptorManager>()
                        .UpdateShellDescriptorAsync(
                            0,
                            environment.Blueprint.Descriptor.Features,
                            environment.Blueprint.Descriptor.Parameters).Wait();
                }
			}

            // In effect "pump messages" see PostMessage circa 1980.
            while (_processingEngine.AreTasksPending())
            {
                _processingEngine.ExecuteNextTask();
            }

            string executionId;
            using (var environment = _orchardHost.CreateShellContext(shellSettings))
            {
                using (var scope = environment.CreateServiceScope())
                {
                    executionId = CreateTenantData(context, environment);
                }
            }

            shellSettings.State = TenantState.Running;
            _runningShellRouterTable.Remove(shellSettings.Name);
            _orchardHost.UpdateShellSettings(shellSettings);
            return executionId;
        }

        private string CreateTenantData(SetupContext context, ShellContext shellContext)
        {
            var recipeManager = shellContext.ServiceProvider.GetService<IRecipeManager>();
            var recipe = context.Recipe;
            var executionId = recipeManager.ExecuteAsync(recipe).Result;

            // Once the recipe has finished executing, we need to update the shell state to "Running", so add a recipe step that does exactly that.
            JObject activateShellJSteps = new JObject();
            JObject activateShellJStep = new JObject();
            activateShellJStep.Add("name", "ActivateShell");
            activateShellJSteps.Add("steps", activateShellJStep);

            var activateShellStep = new RecipeStep(
                Guid.NewGuid().ToString("N"), 
                recipe.Name, 
                "ActivateShell",
                activateShellJSteps);

            recipeManager.ExecuteRecipeStep(executionId, activateShellStep);

            return executionId;
        }
    }
}