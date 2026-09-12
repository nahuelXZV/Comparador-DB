using System.Collections.ObjectModel;
using Application.Abstractions;
using Application.Models;
using Desktop.Services;

namespace Desktop.ViewModels;

public sealed class ComparisonViewModel : ViewModelBase
{
    private readonly IComparisonWorkflowService _comparisonWorkflowService;
    private readonly IClipboardService _clipboardService;
    private readonly IScriptFileSaveService _scriptFileSaveService;
    private ScriptGenerationMode _selectedGenerationMode = ScriptGenerationMode.SafeUpdate;
    private string _excludedTablePatterns = string.Empty;
    private TableNameFilterMode _selectedTableNameFilterMode = TableNameFilterMode.StartsWith;
    private bool _isTableNameFilterEnabled;
    private bool _compareMissingTables = true;
    private bool _compareMissingColumns = true;
    private bool _skipTablesWithDestinationOnlyColumns = true;
    private string _copyFeedback = string.Empty;
    private string _comparisonSummary = "Aún no se realizó una comparación.";
    private string _generatedScript = "Configure ambas conexiones, seleccione los esquemas y presione ‘Comparar’.";
    private bool _isComparing;
    private CancellationTokenSource? _comparisonCancellationSource;

    public ComparisonViewModel(
        ObservableCollection<SavedConnectionProfile> savedConnections,
        IConnectionManagementService connectionManagementService,
        IConnectionDiscoveryService connectionDiscoveryService,
        IComparisonWorkflowService comparisonWorkflowService,
        IClipboardService clipboardService,
        IScriptFileSaveService scriptFileSaveService,
        IConnectionTestDialogService connectionTestDialogService)
    {
        _comparisonWorkflowService = comparisonWorkflowService;
        _clipboardService = clipboardService;
        _scriptFileSaveService = scriptFileSaveService;
        Origin = new ComparisonConnectionViewModel(
            "Base origen",
            savedConnections,
            connectionManagementService,
            connectionDiscoveryService,
            connectionTestDialogService);
        Destination = new ComparisonConnectionViewModel(
            "Base destino",
            savedConnections,
            connectionManagementService,
            connectionDiscoveryService,
            connectionTestDialogService);
        CompareCommand = new AsyncRelayCommand(CompareAsync);
        CancelComparisonCommand = new RelayCommand(CancelComparison, () => IsComparing);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
    }

    public ComparisonConnectionViewModel Origin { get; }
    public ComparisonConnectionViewModel Destination { get; }
    public ObservableCollection<string> Warnings { get; } = [];
    public AsyncRelayCommand CompareCommand { get; }
    public RelayCommand CancelComparisonCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ClearCommand { get; }

    public ScriptGenerationMode SelectedGenerationMode { get => _selectedGenerationMode; set => SetField(ref _selectedGenerationMode, value); }
    public string ExcludedTablePatterns { get => _excludedTablePatterns; set => SetField(ref _excludedTablePatterns, value); }
    public TableNameFilterMode SelectedTableNameFilterMode { get => _selectedTableNameFilterMode; set => SetField(ref _selectedTableNameFilterMode, value); }
    public bool IsTableNameFilterEnabled { get => _isTableNameFilterEnabled; set => SetField(ref _isTableNameFilterEnabled, value); }
    public bool CompareMissingTables { get => _compareMissingTables; set => SetField(ref _compareMissingTables, value); }
    public bool CompareMissingColumns { get => _compareMissingColumns; set => SetField(ref _compareMissingColumns, value); }
    public bool SkipTablesWithDestinationOnlyColumns { get => _skipTablesWithDestinationOnlyColumns; set => SetField(ref _skipTablesWithDestinationOnlyColumns, value); }
    public string CopyFeedback { get => _copyFeedback; private set => SetField(ref _copyFeedback, value); }
    public string ComparisonSummary { get => _comparisonSummary; private set => SetField(ref _comparisonSummary, value); }
    public string GeneratedScript { get => _generatedScript; private set => SetField(ref _generatedScript, value); }

    public bool IsComparing
    {
        get => _isComparing;
        private set
        {
            if (SetField(ref _isComparing, value))
                CancelComparisonCommand.RaiseCanExecuteChanged();
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
                Origin.Form.ToSettings(),
                Destination.Form.ToSettings(),
                Origin.Form.Schema,
                Destination.Form.Schema,
                CompareMissingTables,
                CompareMissingColumns,
                tableFilter,
                SelectedGenerationMode,
                SkipTablesWithDestinationOnlyColumns), cancellationSource.Token);

            GeneratedScript = result.GeneratedScript.Script;
            ConnectionFormViewModel.Replace(Warnings, result.GeneratedScript.Warnings);
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

    private void CancelComparison() => _comparisonCancellationSource?.Cancel();

    private Task CopyAsync()
    {
        _clipboardService.SetText(GeneratedScript);
        CopyFeedback = "¡Copiado!";
        return Task.CompletedTask;
    }

    private Task SaveAsync()
    {
        if (_scriptFileSaveService.Save(GeneratedScript, "actualizacion-segura.sql"))
            ComparisonSummary = "Script guardado.";
        return Task.CompletedTask;
    }

    private Task ClearAsync()
    {
        Origin.Reset();
        Destination.Reset();
        SelectedGenerationMode = ScriptGenerationMode.SafeUpdate;
        ExcludedTablePatterns = string.Empty;
        SelectedTableNameFilterMode = TableNameFilterMode.StartsWith;
        IsTableNameFilterEnabled = false;
        CompareMissingTables = true;
        CompareMissingColumns = true;
        SkipTablesWithDestinationOnlyColumns = true;
        Warnings.Clear();
        ComparisonSummary = "Aún no se realizó una comparación.";
        GeneratedScript = "Configure ambas conexiones, seleccione los esquemas y presione ‘Comparar’.";
        CopyFeedback = string.Empty;
        return Task.CompletedTask;
    }
}
