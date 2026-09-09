using Application.Abstractions;
using Application.Models;
using Microsoft.Data.SqlClient;

namespace Infrastructure;

public sealed class ConnectionStringFactory : IConnectionStringFactory
{
    public string Create(DatabaseConnectionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Server))
            throw new InvalidOperationException("Debe indicar el servidor.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            InitialCatalog = string.IsNullOrWhiteSpace(settings.DatabaseName) ? "master" : settings.DatabaseName.Trim(),
            ConnectTimeout = 10,
            Encrypt = true,
            TrustServerCertificate = true,
            ApplicationName = "Comparador"
        };

        if (string.IsNullOrWhiteSpace(settings.UserName))
            throw new InvalidOperationException("Debe indicar el usuario.");

        builder.UserID = settings.UserName.Trim();
        builder.Password = settings.Password;

        return builder.ConnectionString;
    }
}
