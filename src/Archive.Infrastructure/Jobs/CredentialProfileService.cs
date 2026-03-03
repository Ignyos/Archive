using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Jobs;

public sealed class CredentialProfileService : ICredentialProfileService
{
    private readonly ArchiveDbContext _dbContext;

    public CredentialProfileService(ArchiveDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> CreateAsync(
        CredentialProviderType providerType,
        string name,
        string secretValue,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(secretValue))
        {
            throw new ArgumentException("Secret value is required.", nameof(secretValue));
        }

        var normalizedName = name.Trim();
        var duplicateExists = await _dbContext.CredentialProfiles
            .AnyAsync(x => x.ProviderType == providerType && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken)
            .ConfigureAwait(false);

        if (duplicateExists)
        {
            throw new InvalidOperationException($"A credential profile named '{normalizedName}' already exists for provider '{providerType}'.");
        }

        var now = DateTime.UtcNow;
        var profile = new CredentialProfile
        {
            Id = Guid.NewGuid(),
            ProviderType = providerType,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ProtectedSecretValue = DpapiSecretProtector.Protect(secretValue),
            CreatedAt = now,
            ModifiedAt = now
        };

        _dbContext.CredentialProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return profile.Id;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        string? secretValue = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var profile = await _dbContext.CredentialProfiles
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return false;
        }

        var normalizedName = name.Trim();
        var duplicateExists = await _dbContext.CredentialProfiles
            .AnyAsync(x => x.Id != id && x.ProviderType == profile.ProviderType && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken)
            .ConfigureAwait(false);

        if (duplicateExists)
        {
            return false;
        }

        profile.Name = normalizedName;
        profile.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (!string.IsNullOrWhiteSpace(secretValue))
        {
            profile.ProtectedSecretValue = DpapiSecretProtector.Protect(secretValue);
        }

        profile.ModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<CredentialProfileItem>> ListAsync(
        CredentialProviderType? providerType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CredentialProfiles
            .AsNoTracking();

        if (providerType.HasValue)
        {
            query = query.Where(x => x.ProviderType == providerType.Value);
        }

        return await query
            .OrderBy(x => x.ProviderType)
            .ThenBy(x => x.Name)
            .Select(x => new CredentialProfileItem(
                x.Id,
                x.ProviderType,
                x.Name,
                x.Description,
                x.CreatedAt,
                x.ModifiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
