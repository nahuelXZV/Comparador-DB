using System.Collections.ObjectModel;
using Application.Abstractions;
using Application.Models;
using Desktop.Services;

namespace Desktop.ViewModels;

public sealed class ComparisonConnectionViewModel : ViewModelBase
{
    private readonly IConnectionManagementService _connectionManagementService;
    private readonly IConnectionDiscoveryService _connectionDiscoveryService;
    private readonly IConnectionTestDialogService _connectionTestDialogService;
    private SavedConnectionProfile? _selectedSavedConnection;

    public ComparisonConnectionViewModel(
        string title,
        ObservableCollection<SavedConnectionProfile> savedConnections,
        IConnectionManagementService connectionManagementService,
        IConnectionDiscoveryService connectionDiscoveryService,
        IConnectionTestDialogService connectionTestDialogService)
    {
        Title = title;
        SavedConnections = savedConnections;
        _connectionManagementService = connectionManagementService;
        _connectionDiscoveryService = connectionDiscoveryService;
        _connectionTestDialogService = connectionTestDialogService;
        Form = new ConnectionFormViewModel { Status = $"Configure la conexión {title.Replace("Base ", string.Empty).ToLowerInvariant()}." };
        LoadDatabasesCommand = new AsyncRelayCommand(TestConnectionAsync);
        LoadSchemasCommand = new AsyncRelayCommand<string>(LoadSchemasAsync);
    }

    public string Title { get; }
    public ConnectionFormViewModel Form { get; }
    public ObservableCollection<SavedConnectionProfile> SavedConnections { get; }
    public AsyncRelayCommand LoadDatabasesCommand { get; }
    public AsyncRelayCommand<string> LoadSchemasCommand { get; }

    public SavedConnectionProfile? SelectedSavedConnection
    {
        get => _selectedSavedConnection;
        set
        {
            if (!SetField(ref _selectedSavedConnection, value) || value is null)
                return;

            _ = ApplySavedConnectionAsync(value);
        }
    }

    public void Reset()
    {
        SelectedSavedConnection = null;
        Form.Reset($"Configure la conexión {Title.Replace("Base ", string.Empty).ToLowerInvariant()}.");
    }

    private async Task ApplySavedConnectionAsync(SavedConnectionProfile profile)
    {
        try
        {
            var details = await _connectionManagementService.GetDetailsAsync(profile.Id)
                ?? throw new InvalidOperationException("La conexión seleccionada ya no existe.");
            if (!Equals(SelectedSavedConnection, profile))
                return;

            Form.Server = details.Profile.Server;
            Form.UserName = details.Profile.UserName;
            Form.Password = details.Password;
            Form.DatabaseName = string.Empty;
            Form.Schema = "dbo";
            Form.Databases.Clear();
            Form.Schemas.Clear();
            await LoadDatabasesAsync(showSuccessDialog: false);

            if (Equals(SelectedSavedConnection, profile) && !string.IsNullOrWhiteSpace(Form.DatabaseName))
                await LoadSchemasAsync(Form.DatabaseName);
        }
        catch (Exception exception)
        {
            Form.Status = $"No se pudo cargar la conexión guardada: {exception.Message}";
        }
    }

    private Task TestConnectionAsync() => LoadDatabasesAsync(showSuccessDialog: true);

    private async Task LoadDatabasesAsync(bool showSuccessDialog)
    {
        try
        {
            Form.Status = "Probando conexión y cargando bases de datos...";
            var databases = await _connectionDiscoveryService.GetDatabasesAsync(Form.ToSettings());
            ConnectionFormViewModel.Replace(Form.Databases, databases);
            if (string.IsNullOrWhiteSpace(Form.DatabaseName) && databases.Count > 0)
                Form.DatabaseName = databases[0];

            Form.Status = $"Conexión correcta. {databases.Count} base(s) disponible(s).";
            if (showSuccessDialog)
                _connectionTestDialogService.ShowSuccess();
        }
        catch (Exception exception)
        {
            Form.Status = $"No se pudieron cargar las bases: {exception.Message}";
        }
    }

    private async Task LoadSchemasAsync(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        try
        {
            Form.DatabaseName = databaseName;
            Form.Status = "Cargando esquemas...";
            var schemas = await _connectionDiscoveryService.GetSchemasAsync(Form.ToSettings());
            ConnectionFormViewModel.Replace(Form.Schemas, schemas);
            Form.Schema = schemas.FirstOrDefault(schema => schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                ?? schemas.FirstOrDefault()
                ?? string.Empty;
            Form.Status = $"Conexión correcta. {schemas.Count} esquema(s) disponible(s).";
        }
        catch (Exception exception)
        {
            Form.Status = $"No se pudo conectar: {exception.Message}";
        }
    }
}
