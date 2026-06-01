namespace MemorySystem.Application.Authentication;

public sealed record IdentityBindingLookup(
    string Provider,
    string Issuer,
    string Subject);
