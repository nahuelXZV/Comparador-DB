using System.Windows;

namespace Desktop.Services;

public sealed class ConnectionTestDialogService : IConnectionTestDialogService
{
    public void ShowSuccess()
    {
        MessageBox.Show(
            System.Windows.Application.Current.MainWindow,
            "Se conectó con éxito.",
            "Conexión correcta",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
