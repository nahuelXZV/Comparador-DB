namespace Application.Models;

/// <summary>Perfil guardado junto con su contraseña recuperada para uso temporal en memoria.</summary>
public sealed record SavedConnectionProfileDetails(SavedConnectionProfile Profile, string Password);
