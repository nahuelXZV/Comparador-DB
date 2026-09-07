using Application.Models;
using Microsoft.Data.SqlClient;

namespace Infrastructure;

public sealed class ConnectionStringFactory
{
    public string Create(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Server))
            throw new InvalidOperationException("Debe indicar el servidor.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = profile.Server.Trim(),
            InitialCatalog = string.IsNullOrWhiteSpace(profile.DatabaseName) ? "master" : profile.DatabaseName.Trim(),
            ConnectTimeout = 10,
            Encrypt = true,
            TrustServerCertificate = profile.TrustServerCertificate,
            ApplicationName = "Comparador"
        };

        if (profile.AuthenticationMode == AuthenticationMode.Windows)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(profile.UserName))
                throw new InvalidOperationException("Debe indicar el usuario.");

            builder.UserID = profile.UserName.Trim();
            builder.Password = profile.Password;
        }

        return builder.ConnectionString;
    }
}
