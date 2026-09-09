using Application.Abstractions;
using Application.Models;

namespace Application.Services;

public sealed class ConnectionManagementService(IConnectionProfileStore profileStore, IConnectionSecretStore secretStore) : IConnectionManagementService
{
    public Task<IReadOnlyList<SavedConnectionProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
        profileStore.GetAllAsync(cancellationToken);

    public async Task<SavedConnectionProfileDetails?> GetDetailsAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = (await profileStore.GetAllAsync(cancellationToken))
            .SingleOrDefault(existing => existing.Id == profileId);
        if (profile is null)
            return null;

        var password = await secretStore.GetPasswordAsync(profileId, cancellationToken) ?? string.Empty;
        return new SavedConnectionProfileDetails(profile, password);
    }

    public async Task<SavedConnectionProfile> SaveAsync(SaveConnectionProfileCommand command, CancellationToken cancellationToken = default)
    {
        var name = command.Name.Trim();
        var server = command.Server.Trim();
        var userName = command.UserName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Debe indicar un nombre para la conexión.");
        if (string.IsNullOrWhiteSpace(server))
            throw new InvalidOperationException("Debe indicar el servidor.");
        if (string.IsNullOrWhiteSpace(userName))
            throw new InvalidOperationException("Debe indicar el usuario.");
        if (string.IsNullOrWhiteSpace(command.Password))
            throw new InvalidOperationException("Debe indicar una contraseña para la conexión.");

        var profiles = await profileStore.GetAllAsync(cancellationToken);
        var id = command.Id ?? Guid.NewGuid();
        var existing = profiles.SingleOrDefault(profile => profile.Id == id);
        var hasDuplicateName = profiles.Any(profile =>
            profile.Id != id &&
            profile.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        if (hasDuplicateName)
            throw new InvalidOperationException("Ya existe una conexión guardada con ese nombre.");

        var now = DateTimeOffset.UtcNow;
        var profileToSave = new SavedConnectionProfile(
            id,
            name,
            server,
            userName,
            existing?.CreatedAt ?? now,
            now);

        await secretStore.SavePasswordAsync(profileToSave.Id, command.Password, cancellationToken);
        await profileStore.SaveAsync(profileToSave, cancellationToken);
        return profileToSave;
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await profileStore.DeleteAsync(profileId, cancellationToken);
        await secretStore.DeletePasswordAsync(profileId, cancellationToken);
    }
}
