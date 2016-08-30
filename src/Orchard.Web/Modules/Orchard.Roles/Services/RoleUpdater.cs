﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orchard.Environment.Extensions;
using Orchard.Environment.Extensions.Models;
using Orchard.Environment.Shell;
using Orchard.Security;
using Orchard.Security.Permissions;

namespace Orchard.Roles.Services
{
    public class RoleUpdater : IFeatureEventHandler
    {
        private readonly IRoleManager _roleManager;
        private readonly IEnumerable<IPermissionProvider> _permissionProviders;
        private readonly ITypeFeatureProvider _typeFeatureProvider;

        public RoleUpdater(
            IRoleManager roleManager,
            IEnumerable<IPermissionProvider> permissionProviders,
            ITypeFeatureProvider typeFeatureProvider,
            ILogger<RoleUpdater> logger)
        {
            _typeFeatureProvider = typeFeatureProvider;
            _roleManager = roleManager;
            _permissionProviders = permissionProviders;

            Logger = logger;
        }

        public ILogger Logger { get; set; }

        void IFeatureEventHandler.Installing(Feature feature)
        {
        }

        void IFeatureEventHandler.Installed(Feature feature)
        {
            AddDefaultRolesForFeatureAsync(feature).Wait();
        }

        void IFeatureEventHandler.Enabling(Feature feature)
        {
        }

        void IFeatureEventHandler.Enabled(Feature feature)
        {
        }

        void IFeatureEventHandler.Disabling(Feature feature)
        {
        }

        void IFeatureEventHandler.Disabled(Feature feature)
        {
        }

        void IFeatureEventHandler.Uninstalling(Feature feature)
        {
        }

        void IFeatureEventHandler.Uninstalled(Feature feature)
        {
        }

        public async Task AddDefaultRolesForFeatureAsync(Feature feature)
        {
            var featureName = feature.Descriptor.Id;

            // when another module is being enabled, locate matching permission providers
            var providersForEnabledModule = _permissionProviders.Where(x => _typeFeatureProvider.GetFeatureForDependency(x.GetType())?.Descriptor?.Id == featureName);

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                if (providersForEnabledModule.Any())
                {
                    Logger.LogDebug($"Configuring default roles for module {featureName}");
                }
                else
                {
                    Logger.LogDebug($"No default roles for module {featureName}");
                }
            }

            foreach (var permissionProvider in providersForEnabledModule)
            {
                // get and iterate stereotypical groups of permissions
                var stereotypes = permissionProvider.GetDefaultStereotypes();
                foreach (var stereotype in stereotypes)
                {

                    // turn those stereotypes into roles
                    var role = await _roleManager.GetRoleByNameAsync(stereotype.Name);
                    if (role == null)
                    {
                        if (Logger.IsEnabled(LogLevel.Information))
                        {
                            Logger.LogInformation($"Defining new role {stereotype.Name} for permission stereotype");
                        }

                        role = await _roleManager.CreateRoleAsync(stereotype.Name);
                    }

                    // and merge the stereotypical permissions into that role
                    var stereotypePermissionNames = (stereotype.Permissions ?? Enumerable.Empty<Permission>()).Select(x => x.Name);
                    var currentPermissionNames = role.RoleClaims.Where(x => x.ClaimType == Permission.ClaimType).Select(x => x.ClaimValue);

                    var distinctPermissionNames = currentPermissionNames
                        .Union(stereotypePermissionNames)
                        .Distinct();


                    // update role if set of permissions has increased
                    var additionalPermissionNames = distinctPermissionNames.Except(currentPermissionNames);

                    if (additionalPermissionNames.Any())
                    {
                        foreach (var permissionName in additionalPermissionNames)
                        {
                            if (Logger.IsEnabled(LogLevel.Debug))
                            {
                                Logger.LogInformation("Default role {0} granted permission {1}", stereotype.Name, permissionName);
                            }

                            role.RoleClaims.Add(new RoleClaim() { ClaimType = Permission.ClaimType, ClaimValue = permissionName });
                        }
                    }
                }
            }

            _roleManager.UpdateRoles();
        }
    }
}
