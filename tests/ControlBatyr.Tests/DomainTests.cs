using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Channels;
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
        Assert.Contains("#### Recorded", documents["origin.reference.md"]);
        Assert.DoesNotContain("#### Recorded", documents["sink.reference.md"]);
        Assert.DoesNotContain("### UnexposedModel", string.Concat(documents.Values));
        Assert.DoesNotContain("### UnownedPublisher", string.Concat(documents.Values));
        Assert.Contains("### OriginOnly", documents["origin.reference.md"]);
        Assert.DoesNotContain("### OriginOnly", documents["sink.reference.md"]);
    }

    [Fact]
    public void Models_reachable_from_multiple_domains_and_their_nested_models_are_shared_once()
    {
        var documents = Documents();
        Assert.Contains("### SharedReading\n", documents["shared.md"]);
        Assert.Contains("### SharedDetails\n", documents["shared.md"]);
        Assert.Contains("[SharedReading](shared.md#type-controlbatyr-tests-sharedreading)", documents["origin.reference.md"]);
        Assert.Contains("[SharedReading](shared.md#type-controlbatyr-tests-sharedreading)", documents["sink.reference.md"]);
        foreach (var model in new[] { "SharedReading", "SharedDetails" })
        {
            Assert.Equal(1, string.Concat(documents.Values).Split($"### {model}\n", StringSplitOptions.None).Length - 1);
        }
    }

    [Fact]
    public void Connections_link_the_event_and_target_domain_with_the_action_name_and_summary()
    {
        var documents = Documents();
        Assert.Contains("[Origin](origin.md) — Provides readings.", documents["index.md"]);
        foreach (var file in new[] { "index.md", "origin.reference.md", "sink.reference.md" })
        {
            var markdown = documents[file];
            Assert.Contains("Origin.Recorded]", markdown);
            Assert.Contains(" → [Sink](sink.md): ReceiveReading — ", markdown);
            Assert.Contains("Record the reading in the receiving domain.", markdown);
        }
        Assert.Contains("origin.reference.md#type-controlbatyr-tests-originpublisher-recorded", documents["index.md"]);
        Assert.DoesNotContain("sink.reference.md#type-controlbatyr-tests-sinkmanager-handleasync", documents["origin.reference.md"]);
        Assert.Contains("<a id=\"type-controlbatyr-tests-sinkmanager-handleasync\"></a>", documents["sink.reference.md"]);
        Assert.DoesNotContain("## Connections", documents["gateway.reference.md"]);
    }

    [Fact]
    public async Task Wiring_awaits_actions_in_fresh_scopes_and_disposes_each_target()
    {
        var (services, recorder, _) = Services();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        provider.ConnectDomains(typeof(DomainTests).Assembly, typeof(DomainTests).Assembly);
        var publisher = provider.GetRequiredService<OriginPublisher>();
        var first = new SharedReading(new SharedDetails("first"));
        var second = new SharedReading(new SharedDetails("second"));

        publisher.NotifyRecorded(first);
        await recorder.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Empty(recorder.Readings);
        Assert.Empty(recorder.Disposed);
        recorder.Release.SetResult();
        await recorder.NextDisposalAsync();
        publisher.NotifyRecorded(second);
        await recorder.NextDisposalAsync();

        Assert.Equal(new[] { first, second }, recorder.Readings);
        Assert.Equal(2, recorder.Instances.Distinct().Count());
        Assert.Equal(recorder.Instances, recorder.Disposed);
        Assert.Same(publisher, provider.GetRequiredService<OriginPublisher>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failing_handler_is_logged_with_its_exception_and_connection_identity(bool throwsSynchronously)
    {
        var (services, recorder, logger) = Services();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        provider.ConnectDomains(typeof(DomainTests).Assembly);

        provider.GetRequiredService<OriginPublisher>().NotifyRejected(
            new SharedReading(new SharedDetails(throwsSynchronously ? "synchronous" : "asynchronous")));
        if (!throwsSynchronously)
        {
            await recorder.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            recorder.Release.SetResult();
        }
        var entry = await logger.Failure.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await recorder.NextDisposalAsync();

        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal("The reading was rejected.", entry.Exception.Message);
        Assert.Contains("TestConnections.RejectReadingAsync", entry.Message);
        Assert.Single(recorder.Disposed);
    }

    [Fact]
    public async Task Existing_singleton_publisher_instances_are_wired_at_startup()
    {
        var (services, recorder, _) = Services();
        var publisher = new OriginPublisher();
        services.AddSingleton(publisher);
        await using var provider = services.BuildServiceProvider();
        provider.ConnectDomains(typeof(DomainTests).Assembly);
        Assert.Same(publisher, provider.GetRequiredService<OriginPublisher>());
        var reading = new SharedReading(new SharedDetails("reading"));

        publisher.NotifyRecorded(reading);
        recorder.Release.SetResult();
        await recorder.NextDisposalAsync();

        Assert.Equal(reading, Assert.Single(recorder.Readings));
    }

    [Fact]
    public void A_scoped_publisher_is_rejected_by_scope_validation_at_startup()
    {
        var (services, _, _) = Services();
        services.AddScoped<OriginPublisher>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var failure = Assert.Throws<InvalidOperationException>(() => provider.ConnectDomains(typeof(DomainTests).Assembly));
        Assert.Contains("OriginPublisher", failure.Message);
        Assert.Contains("scoped", failure.Message);
    }

    [Theory]
    [InlineData("Missing", typeof(SinkManager), typeof(SharedReading), typeof(Task), true)]
    [InlineData("Recorded", typeof(DeliveryRecorder), typeof(SharedReading), typeof(Task), true)]
    [InlineData("Recorded", typeof(SinkManager), typeof(string), typeof(Task), true)]
    [InlineData("Recorded", typeof(SinkManager), typeof(SharedReading), typeof(void), true)]
    [InlineData("Recorded", typeof(SinkManager), typeof(SharedReading), typeof(Task), false)]
    public void Invalid_connections_fail_at_startup_with_the_connection_and_event_identity(
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

        using var provider = new ServiceCollection().BuildServiceProvider();
        var failure = Assert.Throws<InvalidOperationException>(() => provider.ConnectDomains(assembly));

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
        services.AddSingleton<OriginPublisher>();
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
public sealed class SinkManager(DeliveryRecorder recorder) : IAsyncDisposable
{
    private readonly Guid instance = Guid.NewGuid();
    public SharedReading Read() => new(new SharedDetails("reading"));
    public async Task HandleAsync(SharedReading payload)
    {
        recorder.Started.TrySetResult();
        await recorder.Release.Task;
        recorder.Readings.Add(payload);
        recorder.Instances.Add(instance);
    }
    public async Task FailAsync(SharedReading payload)
    {
        recorder.Started.TrySetResult();
        await recorder.Release.Task;
        throw new InvalidOperationException("The reading was rejected.");
    }
    public ValueTask DisposeAsync()
    {
        recorder.Disposed.Add(instance);
        recorder.Completions.Writer.TryWrite(instance);
        return ValueTask.CompletedTask;
    }
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
    internal void NotifyRecorded(SharedReading reading) => Recorded?.Invoke(reading);
    internal void NotifyRejected(SharedReading reading) => Rejected?.Invoke(reading);
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
    internal static Task ReceiveReadingAsync(SinkManager manager, SharedReading payload) => manager.HandleAsync(payload);

    [On<OriginPublisher>(nameof(OriginPublisher.Rejected))]
    internal static Task RejectReadingAsync(SinkManager manager, SharedReading payload) => payload.Details.Label == "synchronous"
        ? throw new InvalidOperationException("The reading was rejected.") : manager.FailAsync(payload);
}

public sealed class DeliveryRecorder
{
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Channel<Guid> Completions { get; } = Channel.CreateUnbounded<Guid>();
    internal Task<Guid> NextDisposalAsync() => Completions.Reader.ReadAsync(TestContext.Current.CancellationToken)
        .AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    internal List<SharedReading> Readings { get; } = [];
    internal List<Guid> Instances { get; } = [];
    internal List<Guid> Disposed { get; } = [];
}

internal sealed class RecordingLogger : ILoggerFactory, ILogger
{
    internal TaskCompletionSource<(Exception Exception, string Message)> Failure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
            Failure.TrySetResult((exception, formatter(state, exception)));
        }
    }
}
