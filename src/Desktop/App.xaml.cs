using System.Windows;
using Application.Abstractions;
using Application.Services;
using Application.Services.Generation;
using Desktop.Services;
using Desktop.ViewModels;
using Infrastructure;

namespace Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IConnectionProfileStore connectionProfileStore = new JsonConnectionProfileStore();
        IConnectionSecretStore connectionSecretStore = new WindowsCredentialConnectionSecretStore();
        IConnectionManagementService connectionManagementService = new ConnectionManagementService(
            connectionProfileStore,
            connectionSecretStore);
        IConnectionStringFactory connectionStringFactory = new ConnectionStringFactory();
        IDatabaseMetadataReader metadataReader = new DatabaseMetadataReader();
        IConnectionDiscoveryService connectionDiscoveryService = new ConnectionDiscoveryService(
            metadataReader,
            connectionStringFactory);
        IComparisonWorkflowService comparisonWorkflowService = new ComparisonWorkflowService(
            metadataReader,
            connectionStringFactory,
            new SchemaComparisonService(),
            new ScriptGenerationStrategyResolver(
            [
                new SafeUpdateScriptGenerationStrategy(),
                new RebuildTableScriptGenerationStrategy()
            ]));

        var shellViewModel = new ShellViewModel(
            connectionManagementService,
            connectionDiscoveryService,
            comparisonWorkflowService,
            new ClipboardService(),
            new ScriptFileSaveService(),
            new ConnectionTestDialogService());
        var mainWindow = new MainWindow(shellViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
