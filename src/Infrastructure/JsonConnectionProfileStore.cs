using System.Text.Json;
using Application.Abstractions;
using Application.Models;

namespace Infrastructure;

public sealed class JsonConnectionProfileStore : IConnectionProfileStore
{
    private readonly string _profilesFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonConnectionProfileStore()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Comparador");
        _profilesFilePath = Path.Combine(folder, "connections.json");
    }

    public async Task<IReadOnlyList<SavedConnectionProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await ReadProfilesAsync(cancellationToken);
            return profiles.OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(SavedConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await ReadProfilesAsync(cancellationToken);
            var existingIndex = profiles.FindIndex(existing => existing.Id == profile.Id);
            if (existingIndex >= 0)
                profiles[existingIndex] = profile;
            else
                profiles.Add(profile);

            await WriteProfilesAsync(profiles, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await ReadProfilesAsync(cancellationToken);
            if (profiles.RemoveAll(profile => profile.Id == profileId) > 0)
                await WriteProfilesAsync(profiles, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<SavedConnectionProfile>> ReadProfilesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_profilesFilePath))
            return [];

        await using var stream = File.OpenRead(_profilesFilePath);
        return await JsonSerializer.DeserializeAsync<List<SavedConnectionProfile>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task WriteProfilesAsync(List<SavedConnectionProfile> profiles, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_profilesFilePath)!;
        Directory.CreateDirectory(directory);
        var temporaryFilePath = _profilesFilePath + ".tmp";

        await using (var stream = File.Create(temporaryFilePath))
            await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, cancellationToken);

        File.Move(temporaryFilePath, _profilesFilePath, true);
    }
}
