using System.Reflection;
using AllenStreetNetDaemonApps;
using AllenStreetNetDaemonApps.EntityWrappers;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using HomeAssistantGenerated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;

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
                .AddSingleton<IKitchenLightsWrapper, KitchenLightsWrapper>()
                .AddSingleton<IFrontRoomLightsWrapper, FrontRoomLightsWrapper>()
                .AddSingleton<IGuestBathLightsWrapper, GuestBathLightsWrapper>()
                .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
                .AddNetDaemonStateManager()
                .AddNetDaemonScheduler()
                .AddHomeAssistantGenerated()
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