namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Hosting.Helpers;
    using Hosting.Profiles;
    using Logging;

    class GenericHost
    {
        public GenericHost(IConfigureThisEndpoint specifier, string[] args, List<Type> defaultProfiles, string endpointName, IEnumerable<string> scannableAssembliesFullName = null)
        {
            this.specifier = specifier;

            if (string.IsNullOrEmpty(endpointName))
            {
                endpointName = specifier.GetType().Namespace ?? specifier.GetType().Assembly.GetName().Name;
            }

            endpointNameToUse = endpointName;
            List<Assembly> assembliesToScan;

            if (scannableAssembliesFullName == null || !scannableAssembliesFullName.Any())
            {
                var assemblyScanner = new AssemblyScanner();
                assembliesToScan = assemblyScanner
                    .GetScannableAssemblies()
                    .Assemblies;
            }
            else
            {
                assembliesToScan = scannableAssembliesFullName
                    .Select(Assembly.Load)
                    .ToList();
            }

            profileManager = new ProfileManager(assembliesToScan, args, defaultProfiles);
        }

        public async Task Start()
        {
            try
            {
                var startableEndpoint = await PerformConfiguration().ConfigureAwait(false);
                endpoint = await startableEndpoint.Start().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger<GenericHost>().Fatal("Exception when starting endpoint.", ex);
                throw;
            }
        }

        public async Task Stop()
        {
            if (endpoint != null)
            {
                await endpoint.Stop().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// When installing as windows service (/install), run infrastructure installers
        /// </summary>
        public Task Install(string username)
        {
            return PerformConfiguration(builder => builder.EnableInstallers(username));
        }

        Task<IStartableEndpoint> PerformConfiguration(Action<EndpointConfiguration> moreConfiguration = null)
        {
            var configuration = new EndpointConfiguration(endpointNameToUse);
            
            configuration.DefineCriticalErrorAction(OnCriticalError);

            moreConfiguration?.Invoke(configuration);

            specifier.Customize(configuration);
            profileManager.ActivateProfileHandlers(configuration);

            return Endpoint.Create(configuration);
        }

        // Windows hosting behavior when critical error occurs is suicide.
        async Task OnCriticalError(ICriticalErrorContext context)
        {
            if (Environment.UserInteractive)
            {
                await Task.Delay(10000).ConfigureAwait(false); // so that user can see on their screen the problem
            }

            Environment.FailFast("The following critical error was encountered by NServiceBus:NServiceBus is shutting down.", context.Exception);
        }

        IEndpointInstance endpoint;
        string endpointNameToUse;
        ProfileManager profileManager;
        IConfigureThisEndpoint specifier;
    }
}