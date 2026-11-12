using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace ControlBatyr;
/// <summary>Wires asynchronous domain connections to publisher events.</summary>
public static class ConnectionServiceProviderExtensions
{
    /// <summary>Call once at startup after registering publishers as singletons and building the provider.
    /// Each event starts an asynchronous action in a fresh scope; failures are logged.</summary>
    public static void ConnectDomains(this IServiceProvider services, params Assembly[] assemblies)
    {
        foreach (var connection in Connection.Find(assemblies))
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ControlBatyr.Connections");
            var factory = typeof(ConnectionServiceProviderExtensions).GetMethod(nameof(Handler), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(connection.Manager, connection.Method.GetParameters()[1].ParameterType)
                .CreateDelegate<Func<IServiceProvider, MethodInfo, ILogger, Delegate>>();
            connection.Event.AddEventHandler(services.GetRequiredService(connection.Publisher), factory(services, connection.Method, logger));
        }
    }
    private static Delegate Handler<TManager, TPayload>(IServiceProvider services, MethodInfo method, ILogger logger) where TManager : notnull
    {
        var action = method.CreateDelegate<Func<TManager, TPayload, Task>>();
        return new Action<TPayload>(payload => _ = RunAsync(payload));
        async Task RunAsync(TPayload payload)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                await action(scope.ServiceProvider.GetRequiredService<TManager>(), payload).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Connection {Connection} failed", $"{method.DeclaringType?.FullName}.{method.Name}");
            }
        }
    }
}
