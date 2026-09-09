namespace Application.Models;

public sealed record SaveConnectionProfileCommand(
    Guid? Id,
    string Name,
    string Server,
    string UserName,
    string Password);
