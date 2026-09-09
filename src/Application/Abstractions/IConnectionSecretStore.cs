namespace Application.Abstractions;

public interface IConnectionSecretStore
{
    Task<string?> GetPasswordAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task SavePasswordAsync(Guid profileId, string password, CancellationToken cancellationToken = default);

    Task DeletePasswordAsync(Guid profileId, CancellationToken cancellationToken = default);
}
