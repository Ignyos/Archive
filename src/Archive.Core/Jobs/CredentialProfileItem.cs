using Archive.Core.Domain.Enums;

namespace Archive.Core.Jobs;

public sealed record CredentialProfileItem(
    Guid Id,
    CredentialProviderType ProviderType,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime ModifiedAt);
