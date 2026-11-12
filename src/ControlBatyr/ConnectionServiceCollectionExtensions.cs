using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlBatyr;

/// <summary>Wires domain connections to singleton publishers.</summary>
public static class ConnectionServiceCollectionExtensions
{
    /// <summary>Finds synchronous [On] actions and subscribes when their singleton publisher is resolved.
    /// Each notification resolves its target manager in a fresh scope; failures are logged.
    /// Register publisher dependencies and logging before building the service provider.</summary>
    public static IServiceCollection AddConnections(this IServiceCollection services, params Assembly[] assemblies)
    {
        foreach (var group in Connection.Find(assemblies).GroupBy(connection => connection.Publisher))
        {
            var publisher = group.Key;
            var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == publisher);
            if (existing is not null && existing.Lifetime != ServiceLifetime.Singleton)
            {
                throw new InvalidOperationException($"Connection publisher {publisher.FullName} must be a singleton.");
            }
            var registration = services.Select(descriptor => descriptor.ImplementationInstance)
                .OfType<PublisherRegistration>().SingleOrDefault(item => item.Publisher == publisher);
            if (registration is null)
            {
                registration = new PublisherRegistration(publisher, existing);
                services.AddSingleton(registration);
                if (existing is not null)
                {
                    services.Remove(existing);
                }
                services.AddSingleton(publisher, registration.Create);
            }
            foreach (var connection in group)
            {
                registration.Connections.Add(connection);
            }
        }
        return services;
    }

    private sealed class PublisherRegistration(Type publisher, ServiceDescriptor? original)
    {
        internal Type Publisher { get; } = publisher;
        internal HashSet<Connection> Connections { get; } = [];

        internal object Create(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ControlBatyr.Connections");
            var scopes = provider.GetRequiredService<IServiceScopeFactory>();
            var instance = original?.ImplementationInstance
                ?? original?.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, original?.ImplementationType ?? Publisher);
            foreach (var connection in Connections)
            {
                var parameters = connection.Event.EventHandlerType!.GetMethod("Invoke")!.GetParameters()
                    .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
                Action<object?> handler = payload =>
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var manager = scope.ServiceProvider.GetRequiredService(connection.Manager);
                        connection.Method.Invoke(null, [manager, payload]);
                    }
                    catch (Exception exception)
                    {
                        var failure = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
                        logger.LogError(failure, "Connection {Connection} failed on {Publisher}.{Event}",
                            $"{connection.Method.DeclaringType?.FullName}.{connection.Method.Name}", Publisher.Name, connection.Event.Name);
                    }
                };
                var body = Expression.Invoke(Expression.Constant(handler), Expression.Convert(parameters[^1], typeof(object)));
                var subscriber = Expression.Lambda(connection.Event.EventHandlerType, body, parameters).Compile();
                connection.Event.AddEventHandler(instance, subscriber);
            }
            return instance;
        }
    }
}
