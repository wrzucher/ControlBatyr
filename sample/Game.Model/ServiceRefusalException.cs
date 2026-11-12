namespace Game.Model;

public sealed class ServiceRefusalException(IReadOnlyList<ServiceErrorCode> codes)
    : Exception("The requested game operation is not allowed.")
{
    public IReadOnlyList<ServiceErrorCode> Codes { get; } = Array.AsReadOnly(codes.ToArray());
}
