using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Jobs;

public sealed class DpapiSecretStore : ISecretStore
{
    private readonly ArchiveDbContext _dbContext;

    public DpapiSecretStore(ArchiveDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SetSecretAsync(string reference, string secretValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Secret reference is required.", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(secretValue))
        {
            throw new ArgumentException("Secret value is required.", nameof(secretValue));
        }

        if (!Guid.TryParse(reference.Trim(), out var profileId))
        {
            throw new InvalidOperationException("Secret reference must be a credential profile id.");
        }

        var profile = await _dbContext.CredentialProfiles
            .FirstOrDefaultAsync(x => x.Id == profileId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            throw new InvalidOperationException($"Credential profile '{profileId}' not found.");
        }

        profile.ProtectedSecretValue = DpapiSecretProtector.Protect(secretValue);
        profile.ModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetSecretAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        if (!Guid.TryParse(reference.Trim(), out var profileId))
        {
            return null;
        }

        var protectedValue = await _dbContext.CredentialProfiles
            .AsNoTracking()
            .Where(x => x.Id == profileId)
            .Select(x => x.ProtectedSecretValue)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        return DpapiSecretProtector.Unprotect(protectedValue);
    }
}
