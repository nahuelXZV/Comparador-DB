namespace Application.Models;

/// <summary>Credenciales temporales necesarias para conectarse a una base de datos.</summary>
public sealed record DatabaseConnectionSettings(
    string Server,
    string UserName,
    string Password,
    string DatabaseName);
