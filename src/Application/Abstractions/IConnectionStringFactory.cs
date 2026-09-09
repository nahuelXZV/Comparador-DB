using Application.Models;

namespace Application.Abstractions;

public interface IConnectionStringFactory
{
    string Create(DatabaseConnectionSettings settings);
}
