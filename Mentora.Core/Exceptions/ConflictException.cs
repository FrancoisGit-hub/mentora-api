namespace Mentora.Core.Exceptions;

public class ConflictException(string message, object? details = null) : Exception(message)
{
    public object? Details { get; } = details;
}
