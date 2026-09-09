using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Application.Abstractions;
using Application.Models;
using Application.Services;
using Microsoft.Win32;

namespace Desktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IConnectionManagementService _connectionManagementService;
    private readonly IConnectionDiscoveryService _connectionDiscoveryService;
    private readonly IComparisonWorkflowService _comparisonWorkflowService;
    private string _originSchema = "dbo";
    private string _destinationSchema = "dbo";
    private string _originStatus = "Configure la conexión origen.";
    private string _destinationStatus = "Configure la conexión destino.";
    private string _comparisonSummary = "Aún no se realizó una comparación.";
    private string _generatedScript = "Configure ambas conexiones, seleccione los esquemas y presione ‘Comparar’.";
    private ScriptGenerationMode _selectedGenerationMode = ScriptGenerationMode.SafeUpdate;
    private string _excludedTablePatterns = string.Empty;
    private TableNameFilterMode _selectedTableNameFilterMode = TableNameFilterMode.StartsWith;
    private bool _isTableNameFilterEnabled;
    private bool _compareMissingTables = true;
    private bool _compareMissingColumns = true;
    private bool _skipTablesWithDestinationOnlyColumns = true;
    private string _copyFeedback = string.Empty;
    private string _managedConnectionName = string.Empty;
    private string _managementStatus = "Cree una conexión o seleccione una existente para editarla.";
    private SavedConnectionProfile? _selectedSavedConnection;
    private SavedConnectionProfile? _selectedOriginSavedConnection;
    private SavedConnectionProfile? _selectedDestinationSavedConnection;
    private bool _isComparing;
    private CancellationTokenSource? _comparisonCancellationSource;

    public MainViewModel(
        IConnectionManagementService connectionManagementService,
        IConnectionDiscoveryService connectionDiscoveryService,
        IComparisonWorkflowService comparisonWorkflowService)
    {
        _connectionManagementService = connectionManagementService;
        _connectionDiscoveryService = connectionDiscoveryService;
        _comparisonWorkflowService = comparisonWorkflowService;
        Origin = new ConnectionProfile();
        Destination = new ConnectionProfile();
        ManagedConnection = new ConnectionProfile();
        LoadOriginDatabasesCommand = new AsyncRelayCommand(() => LoadDatabasesAsync(Origin, true));
        LoadDestinationDatabasesCommand = new AsyncRelayCommand(() => LoadDatabasesAsync(Destination, false));
        CompareCommand = new AsyncRelayCommand(CompareAsync);
        CancelComparisonCommand = new RelayCommand(CancelComparison, () => IsComparing);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
        NewManagedConnectionCommand = new AsyncRelayCommand(NewManagedConnectionAsync);
        SaveManagedConnectionCommand = new AsyncRelayCommand(SaveManagedConnectionAsync);
        DeleteManagedConnectionCommand = new AsyncRelayCommand(DeleteManagedConnectionAsync);
        TestManagedConnectionCommand = new AsyncRelayCommand(TestManagedConnectionAsync);
        RefreshManagedConnectionsCommand = new AsyncRelayCommand(LoadSavedConnectionsAsync);
        _ = LoadSavedConnectionsAsync();
    }

    public ConnectionProfile Origin { get; }
    public ConnectionProfile Destination { get; }
    public ConnectionProfile ManagedConnection { get; }
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<SavedConnectionProfile> SavedConnections { get; } = [];
    public AsyncRelayCommand LoadOriginDatabasesCommand { get; }
    public AsyncRelayCommand LoadDestinationDatabasesCommand { get; }
    public AsyncRelayCommand CompareCommand { get; }
    public RelayCommand CancelComparisonCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ClearCommand { get; }
    public AsyncRelayCommand NewManagedConnectionCommand { get; }
    public AsyncRelayCommand SaveManagedConnectionCommand { get; }
    public AsyncRelayCommand DeleteManagedConnectionCommand { get; }
    public AsyncRelayCommand TestManagedConnectionCommand { get; }
    public AsyncRelayCommand RefreshManagedConnectionsCommand { get; }

    public event EventHandler? ManagedConnectionPasswordChanged;
    public event EventHandler? OriginSavedConnectionApplied;
    public event EventHandler? DestinationSavedConnectionApplied;

    public string ManagedConnectionName
    {
        get => _managedConnectionName;
        set => SetField(ref _managedConnectionName, value);
    }

    public string ManagementStatus
    {
        get => _managementStatus;
        private set => SetField(ref _managementStatus, value);
    }

    public SavedConnectionProfile? SelectedSavedConnection
    {
        get => _selectedSavedConnection;
        set
        {
            if (!SetField(ref _selectedSavedConnection, value))
                return;

            _ = LoadSelectedSavedConnectionAsync(value);
        }
    }

    public SavedConnectionProfile? SelectedOriginSavedConnection
    {
        get => _selectedOriginSavedConnection;
        set
        {
            if (!SetField(ref _selectedOriginSavedConnection, value) || value is null)
                return;

            _ = ApplySavedConnectionToComparisonAsync(value, Origin, true);
        }
    }

    public SavedConnectionProfile? SelectedDestinationSavedConnection
    {
        get => _selectedDestinationSavedConnection;
        set
        {
            if (!SetField(ref _selectedDestinationSavedConnection, value) || value is null)
                return;

            _ = ApplySavedConnectionToComparisonAsync(value, Destination, false);
        }
    }

    public ScriptGenerationMode SelectedGenerationMode
    {
        get => _selectedGenerationMode;
        set => SetField(ref _selectedGenerationMode, value);
    }

    public string ExcludedTablePatterns
    {
        get => _excludedTablePatterns;
        set => SetField(ref _excludedTablePatterns, value);
    }

    public TableNameFilterMode SelectedTableNameFilterMode
    {
        get => _selectedTableNameFilterMode;
        set => SetField(ref _selectedTableNameFilterMode, value);
    }

    public bool IsTableNameFilterEnabled
    {
        get => _isTableNameFilterEnabled;
        set => SetField(ref _isTableNameFilterEnabled, value);
    }

    public bool CompareMissingTables
    {
        get => _compareMissingTables;
        set => SetField(ref _compareMissingTables, value);
    }

    public bool CompareMissingColumns
    {
        get => _compareMissingColumns;
        set => SetField(ref _compareMissingColumns, value);
    }

    public bool SkipTablesWithDestinationOnlyColumns
    {
        get => _skipTablesWithDestinationOnlyColumns;
        set => SetField(ref _skipTablesWithDestinationOnlyColumns, value);
    }

    public string CopyFeedback
    {
        get => _copyFeedback;
        private set => SetField(ref _copyFeedback, value);
    }

    public bool IsComparing
    {
        get => _isComparing;
        private set
        {
            if (!SetField(ref _isComparing, value))
                return;

            CancelComparisonCommand.RaiseCanExecuteChanged();
        }
    }

    public string OriginSchema
    {
        get => _originSchema;
        set => SetField(ref _originSchema, value);
    }

    public string DestinationSchema
    {
        get => _destinationSchema;
        set => SetField(ref _destinationSchema, value);
    }

    public string OriginStatus
    {
        get => _originStatus;
        private set => SetField(ref _originStatus, value);
    }

    public string DestinationStatus
    {
        get => _destinationStatus;
        private set => SetField(ref _destinationStatus, value);
    }

    public string ComparisonSummary
    {
        get => _comparisonSummary;
        private set => SetField(ref _comparisonSummary, value);
    }

    public string GeneratedScript
    {
        get => _generatedScript;
        private set => SetField(ref _generatedScript, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task LoadDatabasesAsync(ConnectionProfile profile, bool isOrigin)
    {
        try
        {
            SetConnectionStatus(isOrigin, "Probando conexión y cargando bases de datos...");
            var databases = await _connectionDiscoveryService.GetDatabasesAsync(CreateConnectionSettings(profile));
            Replace(profile.Databases, databases);
            if (string.IsNullOrWhiteSpace(profile.DatabaseName) && databases.Count > 0)
                profile.DatabaseName = databases[0];

            SetConnectionStatus(isOrigin, $"Conexión correcta. {databases.Count} base(s) disponible(s).");
        }
        catch (Exception exception)
        {
            SetConnectionStatus(isOrigin, $"No se pudieron cargar las bases: {exception.Message}");
        }
    }

    public Task LoadOriginSchemasAsync(string databaseName) => LoadSchemasAsync(Origin, true, databaseName);

    public Task LoadDestinationSchemasAsync(string databaseName) => LoadSchemasAsync(Destination, false, databaseName);

    private async Task LoadSchemasAsync(ConnectionProfile profile, bool isOrigin, string? selectedDatabaseName = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(selectedDatabaseName))
                profile.DatabaseName = selectedDatabaseName;

            if (string.IsNullOrWhiteSpace(profile.DatabaseName))
                throw new InvalidOperationException("Seleccione o escriba la base de datos.");

            SetConnectionStatus(isOrigin, "Cargando esquemas...");
            var schemas = await _connectionDiscoveryService.GetSchemasAsync(CreateConnectionSettings(profile));
            Replace(profile.Schemas, schemas);
            var selectedSchema = schemas.FirstOrDefault(schema => schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                ?? schemas.FirstOrDefault()
                ?? string.Empty;
            if (isOrigin)
                OriginSchema = selectedSchema;
            else
                DestinationSchema = selectedSchema;

            SetConnectionStatus(isOrigin, $"Conexión correcta. {schemas.Count} esquema(s) disponible(s).");
        }
        catch (Exception exception)
        {
            SetConnectionStatus(isOrigin, $"No se pudo conectar: {exception.Message}");
        }
    }

    private async Task CompareAsync()
    {
        Warnings.Clear();
        using var cancellationSource = new CancellationTokenSource();
        _comparisonCancellationSource = cancellationSource;
        IsComparing = true;
        try
        {
            ComparisonSummary = "Leyendo estructuras...";
            var tableFilter = IsTableNameFilterEnabled
                ? new TableNameFilter(SelectedTableNameFilterMode, ExcludedTablePatterns)
                : null;
            var result = await _comparisonWorkflowService.CompareAsync(new ComparisonWorkflowRequest(
                CreateConnectionSettings(Origin),
                CreateConnectionSettings(Destination),
                OriginSchema,
                DestinationSchema,
                CompareMissingTables,
                CompareMissingColumns,
                tableFilter,
                SelectedGenerationMode,
                SkipTablesWithDestinationOnlyColumns), cancellationSource.Token);
            GeneratedScript = result.GeneratedScript.Script;
            Replace(Warnings, result.GeneratedScript.Warnings);
            var excludedTables = result.ExcludedOriginTables + result.ExcludedDestinationTables;
            var excludedSummary = excludedTables > 0
                ? $" Se omitieron {excludedTables} tabla(s) por filtro (origen: {result.ExcludedOriginTables}, destino: {result.ExcludedDestinationTables})."
                : string.Empty;
            ComparisonSummary = $"{result.Comparison.MissingTables.Count} tabla(s), {result.Comparison.MissingColumns.Count} columna(s), {result.GeneratedScript.GeneratedStatements} instrucción(es) generada(s).{excludedSummary}";
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            ComparisonSummary = "Comparación cancelada.";
        }
        catch (Exception exception)
        {
            GeneratedScript = "-- No se pudo generar el script. Revise las conexiones y los esquemas.\n";
            Warnings.Add(exception.Message);
            ComparisonSummary = "Comparación no completada.";
        }
        finally
        {
            _comparisonCancellationSource = null;
            IsComparing = false;
        }
    }

    private void CancelComparison()
    {
        _comparisonCancellationSource?.Cancel();
    }

    private Task CopyAsync()
    {
        Clipboard.SetText(GeneratedScript);
        CopyFeedback = "¡Copiado!";
        return Task.CompletedTask;
    }

    private Task ClearAsync()
    {
        SelectedOriginSavedConnection = null;
        SelectedDestinationSavedConnection = null;
        ResetConnectionProfile(Origin);
        ResetConnectionProfile(Destination);
        OriginSchema = "dbo";
        DestinationSchema = "dbo";
        SelectedGenerationMode = ScriptGenerationMode.SafeUpdate;
        ExcludedTablePatterns = string.Empty;
        SelectedTableNameFilterMode = TableNameFilterMode.StartsWith;
        IsTableNameFilterEnabled = false;
        CompareMissingTables = true;
        CompareMissingColumns = true;
        SkipTablesWithDestinationOnlyColumns = true;
        Warnings.Clear();
        OriginStatus = "Configure la conexión origen.";
        DestinationStatus = "Configure la conexión destino.";
        ComparisonSummary = "Aún no se realizó una comparación.";
        GeneratedScript = "Configure ambas conexiones, seleccione los esquemas y presione ‘Comparar’.";
        CopyFeedback = string.Empty;
        return Task.CompletedTask;
    }

    private Task NewManagedConnectionAsync()
    {
        SelectedSavedConnection = null;
        ResetManagedConnectionEditor();
        return Task.CompletedTask;
    }

    private async Task SaveManagedConnectionAsync()
    {
        try
        {
            var profile = await _connectionManagementService.SaveAsync(new SaveConnectionProfileCommand(
                SelectedSavedConnection?.Id,
                ManagedConnectionName,
                ManagedConnection.Server,
                ManagedConnection.UserName,
                ManagedConnection.Password));
            await LoadSavedConnectionsAsync();
            SelectedSavedConnection = SavedConnections.Single(savedConnection => savedConnection.Id == profile.Id);
            ManagementStatus = $"La conexión '{profile.Name}' fue guardada.";
        }
        catch (Exception exception)
        {
            ManagementStatus = $"No se pudo guardar la conexión: {exception.Message}";
        }
    }

    private async Task DeleteManagedConnectionAsync()
    {
        if (SelectedSavedConnection is null)
        {
            ManagementStatus = "Seleccione una conexión para eliminarla.";
            return;
        }

        try
        {
            var profile = SelectedSavedConnection;
            await _connectionManagementService.DeleteAsync(profile.Id);
            SelectedSavedConnection = null;
            ResetManagedConnectionEditor();
            await LoadSavedConnectionsAsync();
            ManagementStatus = $"La conexión '{profile.Name}' fue eliminada.";
        }
        catch (Exception exception)
        {
            ManagementStatus = $"No se pudo eliminar la conexión: {exception.Message}";
        }
    }

    private async Task TestManagedConnectionAsync()
    {
        try
        {
            ManagementStatus = "Probando conexión y cargando bases de datos...";
            var databases = await _connectionDiscoveryService.GetDatabasesAsync(CreateConnectionSettings(ManagedConnection));

            ManagementStatus = $"Conexión correcta. {databases.Count} base(s) disponible(s).";
        }
        catch (Exception exception)
        {
            ManagementStatus = $"No se pudo conectar: {exception.Message}";
        }
    }

    private async Task LoadSavedConnectionsAsync()
    {
        try
        {
            var profiles = await _connectionManagementService.GetAllAsync();
            Replace(SavedConnections, profiles);
        }
        catch (Exception exception)
        {
            ManagementStatus = $"No se pudieron cargar las conexiones guardadas: {exception.Message}";
        }
    }

    private async Task LoadSelectedSavedConnectionAsync(SavedConnectionProfile? profile)
    {
        if (profile is null)
        {
            ResetManagedConnectionEditor();
            return;
        }

        try
        {
            var details = await _connectionManagementService.GetDetailsAsync(profile.Id)
                ?? throw new InvalidOperationException("La conexión seleccionada ya no existe.");
            ManagedConnectionName = details.Profile.Name;
            ManagedConnection.Server = details.Profile.Server;
            ManagedConnection.UserName = details.Profile.UserName;
            SetManagedConnectionPassword(details.Password);
            ManagementStatus = $"Editando la conexión '{details.Profile.Name}'.";
        }
        catch (Exception exception)
        {
            ManagementStatus = $"No se pudo cargar la conexión: {exception.Message}";
        }
    }

    private async Task ApplySavedConnectionToComparisonAsync(SavedConnectionProfile profile, ConnectionProfile target, bool isOrigin)
    {
        try
        {
            var details = await _connectionManagementService.GetDetailsAsync(profile.Id)
                ?? throw new InvalidOperationException("La conexión seleccionada ya no existe.");
            var selectedProfile = isOrigin ? SelectedOriginSavedConnection : SelectedDestinationSavedConnection;
            if (!Equals(selectedProfile, profile))
                return;

            target.Server = details.Profile.Server;
            target.UserName = details.Profile.UserName;
            target.Password = details.Password;
            target.DatabaseName = string.Empty;
            target.Databases.Clear();
            target.Schemas.Clear();

            if (isOrigin)
            {
                OriginSchema = "dbo";
                OriginSavedConnectionApplied?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                DestinationSchema = "dbo";
                DestinationSavedConnectionApplied?.Invoke(this, EventArgs.Empty);
            }

            await LoadDatabasesAsync(target, isOrigin);
            var stillSelected = isOrigin
                ? Equals(SelectedOriginSavedConnection, profile)
                : Equals(SelectedDestinationSavedConnection, profile);
            if (stillSelected && !string.IsNullOrWhiteSpace(target.DatabaseName))
                await LoadSchemasAsync(target, isOrigin, target.DatabaseName);
        }
        catch (Exception exception)
        {
            SetConnectionStatus(isOrigin, $"No se pudo cargar la conexión guardada: {exception.Message}");
        }
    }

    private void ResetManagedConnectionEditor()
    {
        ManagedConnectionName = string.Empty;
        ResetConnectionProfile(ManagedConnection);
        SetManagedConnectionPassword(string.Empty);
        ManagementStatus = "Cree una conexión o seleccione una existente para editarla.";
    }

    private void SetManagedConnectionPassword(string password)
    {
        ManagedConnection.Password = password;
        ManagedConnectionPasswordChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task SaveAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar script SQL",
            Filter = "Script SQL (*.sql)|*.sql",
            FileName = "actualizacion-segura.sql"
        };

        if (dialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, GeneratedScript, Encoding.UTF8);
            ComparisonSummary = "Script guardado.";
        }

        return Task.CompletedTask;
    }

    private static DatabaseConnectionSettings CreateConnectionSettings(ConnectionProfile profile) => new(
        profile.Server,
        profile.UserName,
        profile.Password,
        profile.DatabaseName);

    private void SetConnectionStatus(bool isOrigin, string message)
    {
        if (isOrigin)
            OriginStatus = message;
        else
            DestinationStatus = message;
    }

    private static void ResetConnectionProfile(ConnectionProfile profile)
    {
        profile.Server = string.Empty;
        profile.UserName = string.Empty;
        profile.Password = string.Empty;
        profile.DatabaseName = string.Empty;
        profile.Databases.Clear();
        profile.Schemas.Clear();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
