using System.Configuration;
using System.Data;
using System.Windows;

using Application.Abstractions;
using Application.Services;
using Application.Services.Generation;
using Desktop.ViewModels;
using Infrastructure;

namespace Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
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
        IConnectionDiscoveryService connectionDiscoveryService = new ConnectionDiscoveryService(metadataReader, connectionStringFactory);
        IComparisonWorkflowService comparisonWorkflowService = new ComparisonWorkflowService(
            metadataReader,
            connectionStringFactory,
            new SchemaComparisonService(),
            new ScriptGenerationStrategyResolver(
            [
                new SafeUpdateScriptGenerationStrategy(),
                new RebuildTableScriptGenerationStrategy()
            ]));
        var mainWindow = new MainWindow(new MainViewModel(
            connectionManagementService,
            connectionDiscoveryService,
            comparisonWorkflowService));
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}

