using System.Reflection;
using System.Reflection.Emit;
using ControlBatyr.Inspection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class DomainTests
{
    private static Dictionary<string, string> Documents() => BusinessApi.Generate(typeof(DomainTests).Assembly)
        .ToDictionary(document => document.RelativePath, document => document.Markdown);

    [Fact]
    public void Constructor_publishers_belong_to_their_manager_and_unexposed_models_stay_hidden()
    {
        var documents = Documents();
        Assert.Contains("#### Recorded", documents["origin.md"]);
        Assert.DoesNotContain("#### Recorded", documents["sink.md"]);
        Assert.DoesNotContain("### UnexposedModel", string.Concat(documents.Values));
        Assert.DoesNotContain("### UnownedPublisher", string.Concat(documents.Values));
        Assert.Contains("### OriginOnly", documents["origin.md"]);
        Assert.DoesNotContain("### OriginOnly", documents["sink.md"]);
    }

    [Fact]
    public void Models_reachable_from_multiple_domains_and_their_nested_models_are_shared_once()
    {
        var documents = Documents();
        Assert.Contains("### SharedReading\n", documents["shared.md"]);
        Assert.Contains("### SharedDetails\n", documents["shared.md"]);
        Assert.Contains("[SharedReading](shared.md#type-controlbatyr-tests-sharedreading)", documents["origin.md"]);
        Assert.Contains("[SharedReading](shared.md#type-controlbatyr-tests-sharedreading)", documents["sink.md"]);
        foreach (var model in new[] { "SharedReading", "SharedDetails" })
        {
            Assert.Equal(1, string.Concat(documents.Values).Split($"### {model}\n", StringSplitOptions.None).Length - 1);
        }
    }

    [Fact]
    public void Connections_link_the_index_and_both_domains_to_the_event_and_operation_with_their_summary()
    {
        var documents = Documents();
        Assert.Contains("[Origin](origin.md) — Provides readings.", documents["index.md"]);
        foreach (var file in new[] { "index.md", "origin.md", "sink.md" })
        {
            var markdown = documents[file];
            Assert.Contains("Origin.Recorded]", markdown);
            Assert.Contains(" → [Sink.Handle]", markdown);
            Assert.Contains("Record the reading in the receiving domain.", markdown);
        }
        Assert.Contains("origin.md#type-controlbatyr-tests-originpublisher-recorded", documents["index.md"]);
        Assert.Contains("sink.md#type-controlbatyr-tests-sinkmanager-handle", documents["origin.md"]);
        Assert.Contains("<a id=\"type-controlbatyr-tests-sinkmanager-handle\"></a>", documents["sink.md"]);
        Assert.DoesNotContain("## Connections", documents["gateway.md"]);
    }

    [Fact]
    public void Wiring_delivers_payloads_in_fresh_scopes_and_disposes_each_target()
    {
        var (services, recorder, _) = Services();
        services.AddConnections(typeof(DomainTests).Assembly, typeof(DomainTests).Assembly);
        services.AddConnections(typeof(DomainTests).Assembly);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var publisher = provider.GetRequiredService<OriginPublisher>();
        var first = new SharedReading(new SharedDetails("first"));
        var second = new SharedReading(new SharedDetails("second"));

        publisher.NotifyRecorded(first);
        publisher.NotifyRecorded(second);

        Assert.Equal(new[] { first, second }, recorder.Readings);
        Assert.Equal(2, recorder.Instances.Distinct().Count());
        Assert.Equal(recorder.Instances, recorder.Disposed);
        Assert.Same(publisher, provider.GetRequiredService<OriginPublisher>());
    }

    [Fact]
    public void A_failing_handler_is_logged_with_its_exception_and_connection_identity()
    {
        var (services, recorder, logger) = Services();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetRequiredService<OriginPublisher>().NotifyRejected(new SharedReading(new SharedDetails("rejected")));

        var entry = Assert.Single(logger.Errors);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal("The reading was rejected.", entry.Exception.Message);
        Assert.Contains("TestConnections.Fail", entry.Message);
        Assert.Contains("OriginPublisher.Rejected", entry.Message);
        Assert.Single(recorder.Disposed);
    }

    [Fact]
    public void Existing_singleton_publisher_instances_are_wired_and_sender_events_are_supported()
    {
        var services = new ServiceCollection();
        var publisher = new OriginPublisher();
        var recorder = new DeliveryRecorder();
        services.AddSingleton(publisher);
        services.AddSingleton(recorder);
        services.AddSingleton<ILoggerFactory>(new RecordingLogger());
        services.AddScoped<SinkManager>();
        services.AddConnections(typeof(DomainTests).Assembly);
        using var provider = services.BuildServiceProvider();
        Assert.Same(publisher, provider.GetRequiredService<OriginPublisher>());
        var reading = new SharedReading(new SharedDetails("sender"));

        publisher.NotifyWithSender(reading);

        Assert.Equal(reading, Assert.Single(recorder.Readings));
    }

    [Fact]
    public void A_scoped_publisher_is_rejected_instead_of_losing_subscriptions_across_scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<OriginPublisher>();
        var failure = Assert.Throws<InvalidOperationException>(() => services.AddConnections(typeof(DomainTests).Assembly));
        Assert.Contains("OriginPublisher must be a singleton", failure.Message);
    }

    [Theory]
    [InlineData("Missing", typeof(SinkManager), typeof(SharedReading), typeof(void), true)]
    [InlineData("Recorded", typeof(DeliveryRecorder), typeof(SharedReading), typeof(void), true)]
    [InlineData("Recorded", typeof(SinkManager), typeof(string), typeof(void), true)]
    [InlineData("Recorded", typeof(SinkManager), typeof(SharedReading), typeof(Task), true)]
    [InlineData("Recorded", typeof(SinkManager), typeof(SharedReading), typeof(void), false)]
    public void Invalid_connections_fail_at_registration_with_the_connection_and_event_identity(
        string eventName, Type manager, Type payload, Type returns, bool isStatic)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("InvalidCore"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Core").DefineType("InvalidConnections", TypeAttributes.Public);
        var method = type.DefineMethod("Handle", MethodAttributes.Public | (isStatic ? MethodAttributes.Static : 0),
            returns, [manager, payload]);
        method.SetCustomAttribute(new CustomAttributeBuilder(typeof(OnAttribute<OriginPublisher>).GetConstructor([typeof(string)])!, [eventName]));
        var body = method.GetILGenerator();
        if (returns != typeof(void))
        {
            body.Emit(OpCodes.Ldnull);
        }
        body.Emit(OpCodes.Ret);
        type.CreateType();

        var failure = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddConnections(assembly));

        Assert.Contains("InvalidConnections.Handle", failure.Message);
        Assert.Contains($"OriginPublisher.{eventName}", failure.Message);
    }

    private static (ServiceCollection Services, DeliveryRecorder Recorder, RecordingLogger Logger) Services()
    {
        var services = new ServiceCollection();
        var recorder = new DeliveryRecorder();
        var logger = new RecordingLogger();
        services.AddSingleton(recorder);
        services.AddSingleton<ILoggerFactory>(logger);
        services.AddScoped<SinkManager>();
        services.AddConnections(typeof(DomainTests).Assembly);
        return (services, recorder, logger);
    }
}

/// <summary>Provides readings.</summary>
[Manager]
public sealed class OriginManager(OriginPublisher publisher)
{
    public SharedReading Read() => new(new SharedDetails("reading"));
    public OriginOnly Local() => new();
    public void Record() => publisher.NotifyRecorded(Read());
}

[Manager]
public sealed class SinkManager(DeliveryRecorder recorder) : IDisposable
{
    private readonly Guid instance = Guid.NewGuid();
    public SharedReading Read() => new(new SharedDetails("reading"));
    public void Handle(SharedReading payload)
    {
        recorder.Readings.Add(payload);
        recorder.Instances.Add(instance);
    }
    public void Fail(SharedReading payload) => throw new InvalidOperationException("The reading was rejected.");
    public void Dispose() => recorder.Disposed.Add(instance);
}

public sealed record SharedReading(SharedDetails Details);
public sealed record SharedDetails(string Label);
public sealed record OriginOnly;
[ObjectModel]
public sealed record UnexposedModel;

[EventPublisher]
public sealed class OriginPublisher
{
    public event Action<SharedReading>? Recorded;
    public event Action<SharedReading>? Rejected;
    public event EventHandler<SharedReading>? WithSender;
    internal void NotifyRecorded(SharedReading reading) => Recorded?.Invoke(reading);
    internal void NotifyRejected(SharedReading reading) => Rejected?.Invoke(reading);
    internal void NotifyWithSender(SharedReading reading) => WithSender?.Invoke(this, reading);
}

[EventPublisher]
public sealed class UnownedPublisher
{
    public event Action<UnexposedModel>? Unused;
    internal void NotifyUnused() => Unused?.Invoke(new UnexposedModel());
}

internal static class TestConnections
{
    /// <summary>Record the reading in the receiving domain.</summary>
    [On<OriginPublisher>(nameof(OriginPublisher.Recorded))]
    [On<OriginPublisher>(nameof(OriginPublisher.WithSender))]
    internal static void Handle(SinkManager manager, SharedReading payload) => manager.Handle(payload);

    [On<OriginPublisher>(nameof(OriginPublisher.Rejected))]
    internal static void Fail(SinkManager manager, SharedReading payload) => manager.Fail(payload);
}

public sealed class DeliveryRecorder
{
    internal List<SharedReading> Readings { get; } = [];
    internal List<Guid> Instances { get; } = [];
    internal List<Guid> Disposed { get; } = [];
}

internal sealed class RecordingLogger : ILoggerFactory, ILogger
{
    internal List<(Exception Exception, string Message)> Errors { get; } = [];
    public ILogger CreateLogger(string categoryName) => this;
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Error && exception is not null)
        {
            Errors.Add((exception, formatter(state, exception)));
        }
    }
}
