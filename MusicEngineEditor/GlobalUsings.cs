// Global using directives to resolve ambiguity between WPF and WinForms

global using MessageBox = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
global using Button = System.Windows.Controls.Button;
global using UserControl = System.Windows.Controls.UserControl;
global using Application = System.Windows.Application;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using TreeViewItem = System.Windows.Controls.TreeViewItem;
global using TabItem = System.Windows.Controls.TabItem;

// Resolve type ambiguities - use WPF types
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using Pen = System.Windows.Media.Pen;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using FontFamily = System.Windows.Media.FontFamily;
global using TextBox = System.Windows.Controls.TextBox;
global using Orientation = System.Windows.Controls.Orientation;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using Timer = System.Threading.Timer;
global using Cursors = System.Windows.Input.Cursors;
