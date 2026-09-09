namespace Application.Models;

/// <summary>
/// Datos no sensibles de una conexión reutilizable. La contraseña se almacena por separado.
/// </summary>
public sealed record SavedConnectionProfile(
    Guid Id,
    string Name,
    string Server,
    string UserName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
