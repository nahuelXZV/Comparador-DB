using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Application.Models;

public sealed class ConnectionProfile : INotifyPropertyChanged
{
    private string _server = string.Empty;
    private AuthenticationMode _authenticationMode = AuthenticationMode.UserPassword;
    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _databaseName = string.Empty;
    private bool _trustServerCertificate = true;

    public string Server
    {
        get => _server;
        set => SetField(ref _server, value);
    }

    public AuthenticationMode AuthenticationMode
    {
        get => _authenticationMode;
        set => SetField(ref _authenticationMode, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetField(ref _userName, value);
    }

    /// <summary>Se mantiene solo en memoria mientras la aplicación está abierta.</summary>
    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    public string DatabaseName
    {
        get => _databaseName;
        set => SetField(ref _databaseName, value);
    }

    public bool TrustServerCertificate
    {
        get => _trustServerCertificate;
        set => SetField(ref _trustServerCertificate, value);
    }

    public ObservableCollection<string> Databases { get; } = [];
    public ObservableCollection<string> Schemas { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
