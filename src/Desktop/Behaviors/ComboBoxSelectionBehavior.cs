using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Desktop.Behaviors;

public static class ComboBoxSelectionBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(ComboBoxSelectionBehavior),
        new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ComboBox comboBox)
            return;

        comboBox.SelectionChanged -= OnSelectionChanged;
        if (args.NewValue is ICommand)
            comboBox.SelectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is not ComboBox comboBox)
            return;

        var command = GetCommand(comboBox);
        var parameter = comboBox.SelectedItem;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}
