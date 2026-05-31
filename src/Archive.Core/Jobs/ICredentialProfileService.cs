using Archive.Core.Domain.Enums;

namespace Archive.Core.Jobs;

public interface ICredentialProfileService
{
    Task<Guid> CreateAsync(
        CredentialProviderType providerType,
        string name,
        string secretValue,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid id,
        string name,
        string? secretValue = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CredentialProfileItem>> ListAsync(
        CredentialProviderType? providerType = null,
        CancellationToken cancellationToken = default);
}
