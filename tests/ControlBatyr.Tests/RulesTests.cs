using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class RulesTests
{
    [Fact]
    public void Managers_cannot_receive_managers_or_service_providers_even_through_private_constructors()
    {
        var errors = Rules.Check(typeof(Coordinator));
        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Contains(": error CB0001:", error));
        Assert.Contains(errors, error => error.Contains(nameof(OtherRole), StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains(nameof(IServiceProvider), StringComparison.Ordinal));
    }

    [Fact]
    public void Manager_inputs_reject_models_product_types_collections_and_generic_parameters()
    {
        var errors = Rules.Check(typeof(InputApi));
        var inputs = errors.Where(error => error.Contains("error CB0002", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, inputs.Length);
        foreach (var name in new[] { "model", "product", "collection", "value" })
        {
            Assert.Contains(inputs, error => error.Contains("parameter " + name, StringComparison.Ordinal));
        }
        Assert.DoesNotContain(errors, error => error.Contains(nameof(InputApi.Simple), StringComparison.Ordinal));
    }

    [Fact]
    public void Persistence_must_be_received_by_exactly_one_constructor_even_on_the_same_manager()
    {
        Assert.Contains("error CB0003", Assert.Single(Rules.Check(typeof(Storage))));
        Assert.Empty(Rules.Check(typeof(Storage), typeof(StorageOwner)));
        Assert.Contains("error CB0003", Assert.Single(Rules.Check(typeof(Storage), typeof(TwoConstructors))));
        Assert.Contains("error CB0003", Assert.Single(Rules.Check(typeof(Storage), typeof(StorageOwner), typeof(SecondOwner))));
    }

    [Fact]
    public void Publishers_reject_generic_methods_and_broad_method_or_event_payloads()
    {
        var errors = Rules.Check(typeof(BroadFeed));
        var broad = errors.Where(error => error.Contains("error CB0004", StringComparison.Ordinal)).ToArray();
        Assert.Equal(5, broad.Length);
        foreach (var name in new[] { "Generic", "Notify", "Batch", "Anything", "BaseEvent" })
        {
            Assert.Contains(broad, error => error.Contains("." + name + ":", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Missing_or_empty_summaries_fail_for_manager_methods_and_publisher_events()
    {
        var errors = Rules.Check(typeof(UndocumentedApi), typeof(UndocumentedFeed));
        Assert.Equal(3, errors.Count);
        Assert.All(errors, error => Assert.Contains("error CB0005", error));
        Assert.Contains(errors, error => error.Contains(".Empty:", StringComparison.Ordinal));
    }

    [Fact]
    public void Object_models_reject_public_setters_and_mutable_fields_but_allow_init_and_readonly_data()
    {
        var errors = Rules.Check(typeof(MutableData));
        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Contains("error CB0006", error));
        Assert.Contains(errors, error => error.Contains(".Property:", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains(".Field:", StringComparison.Ordinal));
        Assert.Empty(Rules.Check(typeof(UnmarkedManager)));
    }

    [Fact]
    public void All_rules_report_together_with_pdb_source_paths_and_member_names()
    {
        var errors = Rules.Check(typeof(Coordinator), typeof(InputApi), typeof(Storage), typeof(BroadFeed),
            typeof(UndocumentedApi), typeof(MutableData));
        foreach (var code in Enumerable.Range(1, 6))
        {
            Assert.Contains(errors, error => error.Contains($": error CB{code:0000}:", StringComparison.Ordinal));
        }
        Assert.All(errors.Where(error => !error.Contains("error CB0003", StringComparison.Ordinal)),
            error => Assert.StartsWith(SourcePath() + ": error CB", error));
        Assert.Contains(errors, error => error.StartsWith(typeof(Storage).Assembly.Location + ": error CB0003:", StringComparison.Ordinal));
    }

    [Fact]
    public void The_sample_core_and_its_attributed_model_and_persistence_dependencies_pass_every_rule()
    {
        var types = ProductTypes.Find(typeof(Game.Core.GameManager).Assembly);
        Assert.Contains(typeof(Game.Model.Bet), types);
        Assert.Contains(typeof(Game.Persistence.GamePersistence), types);
        Assert.Empty(Rules.Check(types));
    }

    private static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Manager]
    public sealed class OtherRole;

    [Manager]
    public sealed class Coordinator
    {
        private Coordinator(OtherRole other, IServiceProvider services) { }
    }

    [Manager]
    public sealed class InputApi
    {
        /// <summary>Accepts supported scalar values.</summary>
        public void Simple(int number, decimal amount, Guid id, DateOnly date, TimeOnly time, DateTime stamp,
            DateTimeOffset offset, TimeSpan duration, string text, bool flag, DayOfWeek day, int? optional, CancellationToken ct) { }
        /// <summary>Demonstrates unsupported inputs.</summary>
        public void Invalid(MutableData model, UnmarkedManager product, IReadOnlyList<string> collection) { }
        /// <summary>Demonstrates an unrestricted input.</summary>
        public void Generic<T>(T value) { }
    }

    [Persistence]
    public sealed class Storage;
    [Manager]
    public sealed class StorageOwner
    {
        public StorageOwner(Storage storage) { }
    }
    [Manager]
    public sealed class SecondOwner
    {
        public SecondOwner(Storage storage) { }
    }
    [Manager]
    public sealed class TwoConstructors
    {
        public TwoConstructors(Storage storage) { }
        public TwoConstructors(Storage storage, int version) { }
    }

    [EventPublisher]
    public sealed class BroadFeed
    {
        public void Generic<T>(T payload) { }
        public void Notify(object payload) { }
        public void Batch(object[] payloads) { }
        /// <summary>Demonstrates an unrestricted event.</summary>
        public event Action<object> Anything { add { } remove { } }
        /// <summary>Demonstrates an unspecified event.</summary>
        public event Action<EventArgs> BaseEvent { add { } remove { } }
    }

    [Manager]
    public sealed class UndocumentedApi
    {
        public void Missing() { }
        /// <summary> </summary>
        public void Empty() { }
    }
    [EventPublisher]
    public sealed class UndocumentedFeed
    {
        public event Action<int> Missing { add { } remove { } }
    }

    [ObjectModel]
    public sealed class MutableData
    {
        public int Property { get; set; }
        public int Field;
        public int Initial { get; init; }
        public int HiddenSetter { get; private set; }
        public readonly int ReadonlyField;
        public const int Constant = 1;
    }
}
