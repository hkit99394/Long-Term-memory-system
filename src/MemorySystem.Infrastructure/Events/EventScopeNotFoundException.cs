namespace MemorySystem.Infrastructure.Events;

public sealed class EventScopeNotFoundException(string message) : Exception(message);
