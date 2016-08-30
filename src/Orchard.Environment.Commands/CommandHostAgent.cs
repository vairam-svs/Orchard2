﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Orchard.Environment.Shell;
using Orchard.Environment.Shell.Models;
using Orchard.Hosting;
using Orchard.Hosting.ShellBuilders;
using Orchard.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Orchard.Environment.Commands
{
    public class CommandHostAgent
    {
        private readonly IOrchardHost _orchardHost;
        private readonly IShellSettingsManager _shellSettingsManager;

        public CommandHostAgent(IOrchardHost orchardHost,
            IShellSettingsManager shellSettingsManager)
        {
            _orchardHost = orchardHost;
            _shellSettingsManager = shellSettingsManager;

            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        public async Task<CommandReturnCodes> RunSingleCommandAsync(TextReader input, TextWriter output, string tenant, string[] args, IDictionary<string, string> switches)
        {
            return await RunCommandAsync(input, output, tenant, args, switches);
        }

        public async Task<CommandReturnCodes> RunCommandAsync(TextReader input, TextWriter output, string tenant, string[] args, IDictionary<string, string> switches)
        {
            try
            {
                tenant = tenant ?? ShellHelper.DefaultShellName;

                using (var env = CreateStandaloneEnvironment(tenant))
                {
                    var commandManager = env.ServiceProvider.GetService<ICommandManager>();

                    var parameters = new CommandParameters
                    {
                        Arguments = args,
                        Switches = switches,
                        Input = input,
                        Output = output
                    };

                    await commandManager.ExecuteAsync(parameters);
                }

                return CommandReturnCodes.Ok;
            }
            catch (OrchardCommandHostRetryException ex)
            {
                // Special "Retry" return code for our host
                await output.WriteLineAsync(T($"{ex.Message} (Retrying...)"));
                return CommandReturnCodes.Retry;
            }
            catch (Exception ex)
            {
                if (ex.IsFatal())
                {
                    throw;
                }
                if (ex is TargetInvocationException &&
                    ex.InnerException != null)
                {
                    // If this is an exception coming from reflection and there is an innerexception which is the actual one, redirect
                    ex = ex.InnerException;
                }
                await OutputExceptionAsync(output, T("Error executing command \"{0}\"", string.Join(" ", args)), ex);
                return CommandReturnCodes.Fail;
            }
        }

        private async Task OutputExceptionAsync(TextWriter output, LocalizedString title, Exception exception)
        {
            // Display header
            await output.WriteLineAsync();
            await output.WriteLineAsync(T($"{title}"));

            // Push exceptions in a stack so we display from inner most to outer most
            var errors = new Stack<Exception>();
            for (var scan = exception; scan != null; scan = scan.InnerException)
            {
                errors.Push(scan);
            }

            // Display inner most exception details
            exception = errors.Peek();
            await output.WriteLineAsync(T("--------------------------------------------------------------------------------"));
            await output.WriteLineAsync();
            await output.WriteLineAsync(T("{0}", exception.Message));
            await output.WriteLineAsync();

            if (!((exception is OrchardException ||
                exception is OrchardCoreException) &&
                exception.InnerException == null))
            {
                await output.WriteLineAsync(T("Exception Details: {0}: {1}", exception.GetType().FullName, exception.Message));
                await output.WriteLineAsync();
                await output.WriteLineAsync(T("Stack Trace:"));
                await output.WriteLineAsync();

                // Display exceptions from inner most to outer most
                foreach (var error in errors)
                {
                    await output.WriteLineAsync(T("[{0}: {1}]", error.GetType().Name, error.Message));
                    await output.WriteLineAsync(T("{0}", error.StackTrace));
                    await output.WriteLineAsync();
                }
            }

            // Display footer
            await output.WriteLineAsync("--------------------------------------------------------------------------------");
            await output.WriteLineAsync();
        }

        private ShellContext CreateStandaloneEnvironment(string tenant)
        {
            // Retrieve settings for speficified tenant.
            var settingsList = _shellSettingsManager.LoadSettings();
            if (settingsList.Any())
            {
                var settings = settingsList.SingleOrDefault(s => string.Equals(s.Name, tenant, StringComparison.OrdinalIgnoreCase));
                if (settings == null)
                {
                    throw new OrchardCoreException(T("Tenant {0} does not exist", tenant));
                }

                return _orchardHost.CreateShellContext(settings);
            }
            else
            {
                // In case of an uninitialized site (no default settings yet), we create a default settings instance.
                var settings = new ShellSettings { Name = ShellHelper.DefaultShellName, State = TenantState.Uninitialized };
                return _orchardHost.CreateShellContext(settings);
            }
        }
    }
}