namespace Archive.Core.Jobs;

public interface ISecretStore
{
    Task SetSecretAsync(string reference, string secretValue, CancellationToken cancellationToken = default);

    Task<string?> GetSecretAsync(string reference, CancellationToken cancellationToken = default);
}
