namespace MemorySystem.Application.Events;

public sealed class EventScopeNotFoundException(string message) : Exception(message);
