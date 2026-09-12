using System.Collections.ObjectModel;
using Application.Abstractions;
using Application.Models;
using Desktop.Services;

namespace Desktop.ViewModels;

public sealed class ConnectionManagementViewModel : ViewModelBase
{
    private readonly IConnectionManagementService _connectionManagementService;
    private readonly IConnectionDiscoveryService _connectionDiscoveryService;
    private readonly IConnectionTestDialogService _connectionTestDialogService;
    private string _connectionName = string.Empty;
    private string _status = "Cree una conexión o seleccione una existente para editarla.";
    private SavedConnectionProfile? _selectedConnection;

    public ConnectionManagementViewModel(
        ObservableCollection<SavedConnectionProfile> savedConnections,
        IConnectionManagementService connectionManagementService,
        IConnectionDiscoveryService connectionDiscoveryService,
        IConnectionTestDialogService connectionTestDialogService)
    {
        SavedConnections = savedConnections;
        _connectionManagementService = connectionManagementService;
        _connectionDiscoveryService = connectionDiscoveryService;
        _connectionTestDialogService = connectionTestDialogService;
        Editor = new ConnectionFormViewModel();
        NewCommand = new AsyncRelayCommand(NewAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        TestCommand = new AsyncRelayCommand(TestAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<SavedConnectionProfile> SavedConnections { get; }
    public ConnectionFormViewModel Editor { get; }
    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand TestCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public string ConnectionName { get => _connectionName; set => SetField(ref _connectionName, value); }
    public string Status { get => _status; private set => SetField(ref _status, value); }

    public SavedConnectionProfile? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (SetField(ref _selectedConnection, value))
                _ = LoadSelectedAsync(value);
        }
    }

    private Task NewAsync()
    {
        SelectedConnection = null;
        ResetEditor();
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        try
        {
            var profile = await _connectionManagementService.SaveAsync(new SaveConnectionProfileCommand(
                SelectedConnection?.Id,
                ConnectionName,
                Editor.Server,
                Editor.UserName,
                Editor.Password));
            await LoadAsync();
            SelectedConnection = SavedConnections.Single(item => item.Id == profile.Id);
            Status = $"La conexión '{profile.Name}' fue guardada.";
        }
        catch (Exception exception)
        {
            Status = $"No se pudo guardar la conexión: {exception.Message}";
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedConnection is null)
        {
            Status = "Seleccione una conexión para eliminarla.";
            return;
        }

        try
        {
            var profile = SelectedConnection;
            await _connectionManagementService.DeleteAsync(profile.Id);
            SelectedConnection = null;
            ResetEditor();
            await LoadAsync();
            Status = $"La conexión '{profile.Name}' fue eliminada.";
        }
        catch (Exception exception)
        {
            Status = $"No se pudo eliminar la conexión: {exception.Message}";
        }
    }

    private async Task TestAsync()
    {
        try
        {
            Status = "Probando conexión...";
            var databases = await _connectionDiscoveryService.GetDatabasesAsync(Editor.ToSettings());
            Status = $"Conexión correcta. {databases.Count} base(s) disponible(s).";
            _connectionTestDialogService.ShowSuccess();
        }
        catch (Exception exception)
        {
            Status = $"No se pudo conectar: {exception.Message}";
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            ConnectionFormViewModel.Replace(SavedConnections, await _connectionManagementService.GetAllAsync());
        }
        catch (Exception exception)
        {
            Status = $"No se pudieron cargar las conexiones guardadas: {exception.Message}";
        }
    }

    private async Task LoadSelectedAsync(SavedConnectionProfile? profile)
    {
        if (profile is null)
        {
            ResetEditor();
            return;
        }

        try
        {
            var details = await _connectionManagementService.GetDetailsAsync(profile.Id)
                ?? throw new InvalidOperationException("La conexión seleccionada ya no existe.");
            if (!Equals(SelectedConnection, profile))
                return;
            ConnectionName = details.Profile.Name;
            Editor.Server = details.Profile.Server;
            Editor.UserName = details.Profile.UserName;
            Editor.Password = details.Password;
            Status = $"Editando la conexión '{details.Profile.Name}'.";
        }
        catch (Exception exception)
        {
            Status = $"No se pudo cargar la conexión: {exception.Message}";
        }
    }

    private void ResetEditor()
    {
        ConnectionName = string.Empty;
        Editor.Reset();
        Status = "Cree una conexión o seleccione una existente para editarla.";
    }
}
