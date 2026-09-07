using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Application.Models;
using Application.Services;
using Application.Services.Generation;
using Infrastructure;
using Microsoft.Win32;

namespace Desktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseMetadataReader _metadataReader = new();
    private readonly ConnectionStringFactory _connectionStringFactory = new();
    private readonly SchemaComparisonService _comparisonService = new();
    private readonly ScriptGenerationStrategyResolver _strategyResolver = new(
    [
        new SafeUpdateScriptGenerationStrategy(),
        new RebuildTableScriptGenerationStrategy()
    ]);
    private string _originSchema = "dbo";
    private string _destinationSchema = "dbo";
    private string _originStatus = "Configure la conexión origen.";
    private string _destinationStatus = "Configure la conexión destino.";
    private string _comparisonSummary = "Aún no se realizó una comparación.";
    private string _generatedScript = "Configure ambas conexiones, seleccione los esquemas y presione ‘Comparar’.";
    private ScriptGenerationMode _selectedGenerationMode = ScriptGenerationMode.SafeUpdate;

    public MainViewModel()
    {
        Origin = new ConnectionProfile();
        Destination = new ConnectionProfile();
        LoadOriginDatabasesCommand = new AsyncRelayCommand(() => LoadDatabasesAsync(Origin, true));
        LoadDestinationDatabasesCommand = new AsyncRelayCommand(() => LoadDatabasesAsync(Destination, false));
        CompareCommand = new AsyncRelayCommand(CompareAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public ConnectionProfile Origin { get; }
    public ConnectionProfile Destination { get; }
    public ObservableCollection<string> Warnings { get; } = [];
    public AsyncRelayCommand LoadOriginDatabasesCommand { get; }
    public AsyncRelayCommand LoadDestinationDatabasesCommand { get; }
    public AsyncRelayCommand CompareCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public ScriptGenerationMode SelectedGenerationMode
    {
        get => _selectedGenerationMode;
        set => SetField(ref _selectedGenerationMode, value);
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
            var databases = await _metadataReader.GetDatabasesAsync(_connectionStringFactory.Create(profile));
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
            var schemas = await _metadataReader.GetSchemasAsync(_connectionStringFactory.Create(profile));
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
        try
        {
            if (string.IsNullOrWhiteSpace(OriginSchema) || string.IsNullOrWhiteSpace(DestinationSchema))
                throw new InvalidOperationException("Seleccione un esquema para ambas bases.");

            ComparisonSummary = "Leyendo estructuras...";
            var originTask = _metadataReader.GetTablesAsync(_connectionStringFactory.Create(Origin), OriginSchema.Trim());
            var destinationTask = _metadataReader.GetTablesAsync(_connectionStringFactory.Create(Destination), DestinationSchema.Trim());
            await Task.WhenAll(originTask, destinationTask);

            var comparison = _comparisonService.Compare(originTask.Result, destinationTask.Result);
            var generated = _strategyResolver.Get(SelectedGenerationMode).Generate(comparison);
            GeneratedScript = generated.Script;
            Replace(Warnings, generated.Warnings);
            ComparisonSummary = $"{comparison.MissingTables.Count} tabla(s), {comparison.MissingColumns.Count} columna(s), {generated.GeneratedStatements} instrucción(es) segura(s).";
        }
        catch (Exception exception)
        {
            GeneratedScript = "-- No se pudo generar el script. Revise las conexiones y los esquemas.\n";
            Warnings.Add(exception.Message);
            ComparisonSummary = "Comparación no completada.";
        }
    }

    private Task CopyAsync()
    {
        Clipboard.SetText(GeneratedScript);
        ComparisonSummary = "Script copiado al portapapeles.";
        return Task.CompletedTask;
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

    private void SetConnectionStatus(bool isOrigin, string message)
    {
        if (isOrigin)
            OriginStatus = message;
        else
            DestinationStatus = message;
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
