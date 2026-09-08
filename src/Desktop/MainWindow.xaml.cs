using System.Windows;
using System.Windows.Controls;
using Desktop.ViewModels;

namespace Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OriginPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Origin.Password = ((PasswordBox)sender).Password;
    }

    private void DestinationPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Destination.Password = ((PasswordBox)sender).Password;
    }

    private void ClearPasswords(object sender, RoutedEventArgs e)
    {
        OriginPasswordBox.Password = string.Empty;
        DestinationPasswordBox.Password = string.Empty;
    }

    private async void OriginDatabaseSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && ((ComboBox)sender).SelectedItem is string databaseName)
            await viewModel.LoadOriginSchemasAsync(databaseName);
    }

    private async void DestinationDatabaseSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && ((ComboBox)sender).SelectedItem is string databaseName)
            await viewModel.LoadDestinationSchemasAsync(databaseName);
    }

}
