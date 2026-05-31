using Archive.Core.Domain.Enums;

namespace Archive.Core.Domain.Entities;

public sealed class CredentialProfile
{
    public Guid Id { get; set; }

    public CredentialProviderType ProviderType { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ProtectedSecretValue { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }
}
