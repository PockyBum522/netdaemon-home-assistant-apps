using System.Reflection;
using AllenStreetNetDaemonApps.Apps.KitchenLightsController;
using AllenStreetNetDaemonApps.Internal;
using AllenStreetNetDaemonApps.Internal.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.Logging;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
// Add next line if using code generator
//using HomeAssistantGenerated;

#pragma warning disable CA1812

try
{
    await Host.CreateDefaultBuilder(args)
        .UseNetDaemonAppSettings()
        .UseNetDaemonDefaultLogging()
        .UseNetDaemonRuntime()
        .UseNetDaemonTextToSpeech()
        .ConfigureServices((_, services) =>
            services
                .AddSingleton<IKitchenLightsControl, KitchenLightsControl>()
                .AddSingleton<IFrontRoomLightsControl, FrontRoomLightsControl>()
                .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
                .AddNetDaemonStateManager()
                .AddNetDaemonScheduler()
                
                // Add next line if using code generator
                // .AddHomeAssistantGenerated()
        )
        .Build()
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to start host... {e}");
    throw;
}