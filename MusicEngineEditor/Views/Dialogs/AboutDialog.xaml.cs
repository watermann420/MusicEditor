using System.Windows;

namespace MusicEngineEditor.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    public static void Show(Window? owner = null)
    {
        var dialog = new AboutDialog
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
