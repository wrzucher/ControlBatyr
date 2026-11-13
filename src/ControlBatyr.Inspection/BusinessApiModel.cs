using System.Collections.Immutable;

namespace ControlBatyr.Inspection;

public sealed record BusinessApiModel(ImmutableArray<BusinessDomain> Domains, ImmutableArray<BusinessModel> Models,
    ImmutableArray<BusinessConnection> Connections);

public sealed record BusinessNode(string Id, string Name, string Summary, string Remarks, string? SourceFile);

public sealed record BusinessDomain(string Name, BusinessNode Node, ImmutableArray<BusinessOperation> Operations,
    ImmutableArray<BusinessPublisher> Publishers);

public sealed record BusinessPublisher(BusinessNode Node, ImmutableArray<BusinessEvent> Events);

public sealed record BusinessOperation(BusinessNode Node, ImmutableArray<BusinessParameter> Parameters,
    TypeReference ReturnType, string Returns, ImmutableArray<BusinessRequirement> Requirements,
    ImmutableArray<string> Raises);

public sealed record BusinessParameter(string Name, TypeReference Type, string Description);

public sealed record BusinessEvent(BusinessNode Node, ImmutableArray<TypeReference> Payloads);

public sealed record BusinessModel(BusinessNode Node, bool IsEnum, ImmutableArray<BusinessProperty> Properties,
    ImmutableArray<BusinessOperation> Methods, ImmutableArray<BusinessEnumValue> Values);

public sealed record BusinessProperty(BusinessNode Node, TypeReference Type, bool Computed);

public sealed record BusinessEnumValue(BusinessNode Node, string Value);

public sealed record BusinessRequirement(BusinessNode Node);

public sealed record BusinessConnection(BusinessNode Node, string EventId, string TargetDomainId);

// An empty name wraps an element type using Suffix; "(" groups tuple elements in Arguments.
public sealed record TypeReference(string Name, string? ModelId, ImmutableArray<TypeReference> Arguments, string Suffix);
