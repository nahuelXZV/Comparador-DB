using System.Collections.ObjectModel;
using Application.Abstractions;
using Application.Models;
using Desktop.Services;

namespace Desktop.ViewModels;

public sealed class ShellViewModel
{
    public ShellViewModel(
        IConnectionManagementService connectionManagementService,
        IConnectionDiscoveryService connectionDiscoveryService,
        IComparisonWorkflowService comparisonWorkflowService,
        IClipboardService clipboardService,
        IScriptFileSaveService scriptFileSaveService,
        IConnectionTestDialogService connectionTestDialogService)
    {
        SavedConnections = [];
        Comparison = new ComparisonViewModel(
            SavedConnections,
            connectionManagementService,
            connectionDiscoveryService,
            comparisonWorkflowService,
            clipboardService,
            scriptFileSaveService,
            connectionTestDialogService);
        ConnectionManagement = new ConnectionManagementViewModel(
            SavedConnections,
            connectionManagementService,
            connectionDiscoveryService,
            connectionTestDialogService);
    }

    public ObservableCollection<SavedConnectionProfile> SavedConnections { get; }
    public ComparisonViewModel Comparison { get; }
    public ConnectionManagementViewModel ConnectionManagement { get; }
}
