namespace MemorySystem.Application.ServiceAccounts;

public interface IServiceAccountLifecycleStore
{
    Task<ServiceAccountProfileRecord> UpsertProfileAsync(
        ServiceAccountProfileCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceAccountCredentialRecord> CreateCredentialAsync(
        ServiceAccountCredentialCreateCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceAccountCredentialRecord> RotateCredentialAsync(
        ServiceAccountCredentialRotationCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceAccountCredentialRecord> DisableCredentialAsync(
        ServiceAccountCredentialDisableCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceAccountNamespaceGrantRecord> GrantNamespaceAsync(
        ServiceAccountNamespaceGrantCommand command,
        CancellationToken cancellationToken = default);
}
