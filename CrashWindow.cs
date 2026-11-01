using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Luba;

// Окно «системы крашей»: причина + советы + полный текст для копирования
public class CrashWindow : Window
{
    public CrashWindow(string title, string advice, string details, string folder)
    {
        Title = "AZURE — " + title;
        Width = 760; Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("Bg");
        FontFamily = new FontFamily("Segoe UI");

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var head = new TextBlock
        {
            Text = title, FontSize = 24, FontWeight = FontWeights.Black,
            Foreground = (Brush)Application.Current.FindResource("Accent")
        };
        root.Children.Add(head);

        var tips = new TextBlock
        {
            Text = "Что могло пойти не так:\n\n" + advice, TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            Margin = new Thickness(0, 12, 0, 12)
        };
        Grid.SetRow(tips, 1);
        root.Children.Add(tips);

        var box = new TextBox
        {
            Text = details, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            TextWrapping = TextWrapping.NoWrap, BorderThickness = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Style = (Style)Application.Current.FindResource("Inp")
        };
        Grid.SetRow(box, 2);
        root.Children.Add(box);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var copy = new Button { Content = "Копировать текст", Style = (Style)Application.Current.FindResource("PrimaryBtn"), Margin = new Thickness(0, 0, 8, 0) };
        copy.Click += (s, e) => { try { Clipboard.SetText(details); } catch { } };
        var open = new Button { Content = "Открыть папку", Style = (Style)Application.Current.FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0) };
        open.Click += (s, e) =>
        {
            try
            {
                var f = Directory.Exists(folder) ? folder : Path.GetDirectoryName(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{f}\"") { UseShellExecute = true });
            }
            catch { }
        };
        var close = new Button { Content = "Закрыть", Style = (Style)Application.Current.FindResource("Btn") };
        close.Click += (s, e) => Close();
        row.Children.Add(copy); row.Children.Add(open); row.Children.Add(close);
        Grid.SetRow(row, 3);
        root.Children.Add(row);

        Content = root;
    }
}
