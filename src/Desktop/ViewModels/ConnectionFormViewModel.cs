using System.Collections.ObjectModel;
using Application.Models;

namespace Desktop.ViewModels;

public sealed class ConnectionFormViewModel : ViewModelBase
{
    private string _server = string.Empty;
    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _databaseName = string.Empty;
    private string _schema = "dbo";
    private string _status = string.Empty;

    public string Server { get => _server; set => SetField(ref _server, value); }
    public string UserName { get => _userName; set => SetField(ref _userName, value); }
    public string Password { get => _password; set => SetField(ref _password, value); }
    public string DatabaseName { get => _databaseName; set => SetField(ref _databaseName, value); }
    public string Schema { get => _schema; set => SetField(ref _schema, value); }
    public string Status { get => _status; set => SetField(ref _status, value); }
    public ObservableCollection<string> Databases { get; } = [];
    public ObservableCollection<string> Schemas { get; } = [];

    public DatabaseConnectionSettings ToSettings() => new(Server, UserName, Password, DatabaseName);

    public void Reset(string status = "")
    {
        Server = string.Empty;
        UserName = string.Empty;
        Password = string.Empty;
        DatabaseName = string.Empty;
        Schema = "dbo";
        Status = status;
        Databases.Clear();
        Schemas.Clear();
    }

    public static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }
}
