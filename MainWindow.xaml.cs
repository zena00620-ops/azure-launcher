using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Luba.Core;

namespace Luba;

public partial class MainWindow : Window
{
    static readonly string[] McVersions =
    {
        "1.20.1", "1.20", "1.19.4", "1.19.3", "1.19.2", "1.19.1", "1.19",
        "1.18.2", "1.18.1", "1.18", "1.17.1", "1.17", "1.16.5"
    };

    static readonly (string name, string hex)[] Accents =
    {
        ("Розовый", "#FF3DA5"), ("Азур", "#2DA8FF"), ("Лайм", "#B6FF3D"),
        ("Ультрафиолет", "#A56BFF"), ("Янтарь", "#FFB02E")
    };

    readonly Settings cfg;
    bool running, loading;

    public MainWindow()
    {
        InitializeComponent();
        Paths.EnsureAll();
        cfg = Settings.Load();

        loading = true;
        ApplyAccent(cfg.Accent);
        BuildAccentChips();
        lbVersions.ItemsSource = McVersions;
        lbVersions.SelectedIndex = 0;
        lbLoaders.ItemsSource = new[] { "Fabric", "Forge", "Vanilla" };
        lbLoaders.SelectedIndex = 0;

        slRam.Maximum = Math.Max(2048, Settings.TotalMb() * 3 / 4 / 256 * 256);
        LoadSettingsUi();
        RefreshAll();
        loading = false;
    }

    // ---------- окно ----------
    void Drag(object s, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    void Min_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object s, RoutedEventArgs e) => Close();

    void Nav_Checked(object s, RoutedEventArgs e)
    {
        if (pgPlay == null || pgSet == null || pgInst == null || pgAcc == null) return;
        var tag = (s as RadioButton)?.Tag as string;
        pgPlay.Visibility = tag == "Play" ? Visibility.Visible : Visibility.Collapsed;
        pgInst.Visibility = tag == "Inst" ? Visibility.Visible : Visibility.Collapsed;
        pgAcc.Visibility = tag == "Acc" ? Visibility.Visible : Visibility.Collapsed;
        pgSet.Visibility = tag == "Set" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- оформление ----------
    void ApplyAccent(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            Application.Current.Resources["AccentColor"] = c;
            Application.Current.Resources["Accent"] = new SolidColorBrush(c);
        }
        catch { }
    }

    void BuildAccentChips()
    {
        foreach (var (name, hex) in Accents)
        {
            var b = new Button
            {
                Width = 44, Height = 44, Margin = new Thickness(0, 0, 10, 0), ToolTip = name, Tag = hex,
                Style = (Style)FindResource("Btn"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
            };
            b.Click += (s, e) =>
            {
                cfg.Accent = (string)((Button)s).Tag;
                ApplyAccent(cfg.Accent);
                cfg.Save();
            };
            panelAccents.Children.Add(b);
        }
    }

    // ---------- данные ----------
    void RefreshAll()
    {
        bool was = loading;
        loading = true;

        lbPlayInstance.ItemsSource = null; lbPlayInstance.ItemsSource = cfg.Instances;
        lbInstances.ItemsSource = null; lbInstances.ItemsSource = cfg.Instances;
        lbPlayAccount.ItemsSource = null; lbPlayAccount.ItemsSource = cfg.Accounts;
        lbAccounts.ItemsSource = null; lbAccounts.ItemsSource = cfg.Accounts;

        var inst = cfg.Instances.FirstOrDefault(i => i.Id == cfg.SelectedInstance) ?? cfg.Instances.FirstOrDefault();
        lbPlayInstance.SelectedItem = inst;
        lbInstances.SelectedItem = inst;
        if (inst != null) cfg.SelectedInstance = inst.Id;

        var acc = cfg.Accounts.FirstOrDefault(a => a.Name == cfg.SelectedAccount) ?? cfg.Accounts.FirstOrDefault();
        lbPlayAccount.SelectedItem = acc;
        lbAccounts.SelectedItem = acc;
        if (acc != null) cfg.SelectedAccount = acc.Name;
        txtUuid.Text = acc != null ? "UUID: " + acc.Uuid : "";

        loading = was;
    }

    void PlayInst_Changed(object s, SelectionChangedEventArgs e)
    {
        if (loading || lbPlayInstance.SelectedItem is not Instance i) return;
        cfg.SelectedInstance = i.Id; cfg.Save();
    }

    void PlayAcc_Changed(object s, SelectionChangedEventArgs e)
    {
        if (loading || lbPlayAccount.SelectedItem is not Account a) return;
        cfg.SelectedAccount = a.Name; cfg.Save();
    }

    void Acc_Changed(object s, SelectionChangedEventArgs e)
    {
        if (lbAccounts.SelectedItem is Account a) txtUuid.Text = "UUID: " + a.Uuid;
    }

    // ---------- сборки ----------
    void CreateInst_Click(object s, RoutedEventArgs e)
    {
        var ver = lbVersions.SelectedItem as string;
        var loader = lbLoaders.SelectedItem as string;
        if (ver == null || loader == null) return;

        var name = tbInstName.Text.Trim();
        if (name == "") name = $"{loader} {ver}";
        var inst = new Instance { Name = name, McVersion = ver, Loader = loader, Optimize = cbOptimize.IsChecked == true };
        Directory.CreateDirectory(Path.Combine(Paths.Instances, inst.Id, "mods"));
        cfg.Instances.Add(inst);
        cfg.SelectedInstance = inst.Id;
        cfg.Save();
        tbInstName.Clear();
        RefreshAll();
    }

    void DelInst_Click(object s, RoutedEventArgs e)
    {
        if (lbInstances.SelectedItem is not Instance inst) return;
        var r = MessageBox.Show($"Удалить сборку «{inst.Name}»?\n\nПапка со всеми мирами и модами этой сборки тоже будет удалена.",
            "AZURE", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        try { Directory.Delete(Path.Combine(Paths.Instances, inst.Id), true); } catch { }
        cfg.Instances.Remove(inst);
        cfg.Save();
        RefreshAll();
    }

    void OpenInst_Click(object s, RoutedEventArgs e)
    {
        if (lbInstances.SelectedItem is Instance inst) OpenFolder(Path.Combine(Paths.Instances, inst.Id));
    }

    void OpenMods_Click(object s, RoutedEventArgs e)
    {
        if (lbInstances.SelectedItem is Instance inst) OpenFolder(Path.Combine(Paths.Instances, inst.Id, "mods"));
    }

    static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    // ---------- аккаунты ----------
    void AddAcc_Click(object s, RoutedEventArgs e)
    {
        var nick = tbNick.Text.Trim();
        if (!Regex.IsMatch(nick, "^[A-Za-z0-9_]{3,16}$"))
        {
            MessageBox.Show("Ник: 3–16 символов, только латиница, цифры и _", "AZURE");
            return;
        }
        if (cfg.Accounts.Any(a => a.Name.Equals(nick, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Такой аккаунт уже есть", "AZURE");
            return;
        }
        var acc = Account.Create(nick);
        cfg.Accounts.Add(acc);
        cfg.SelectedAccount = acc.Name;
        cfg.Save();
        tbNick.Clear();
        RefreshAll();
    }

    void DelAcc_Click(object s, RoutedEventArgs e)
    {
        if (lbAccounts.SelectedItem is not Account acc) return;
        if (cfg.Accounts.Count <= 1) { MessageBox.Show("Нужен хотя бы один аккаунт", "AZURE"); return; }
        var r = MessageBox.Show($"Удалить аккаунт «{acc.Name}»?", "AZURE", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        cfg.Accounts.Remove(acc);
        cfg.Save();
        RefreshAll();
    }

    // ---------- настройки ----------
    void LoadSettingsUi()
    {
        slRam.Value = Math.Clamp(cfg.RamMb, (int)slRam.Minimum, (int)slRam.Maximum);
        txtRam.Text = $"ОЗУ: {(int)slRam.Value} МБ";
        tbJvm.Text = cfg.ExtraJvm;
        tbJava.Text = cfg.JavaPath;
        tbW.Text = cfg.Width.ToString();
        tbH.Text = cfg.Height.ToString();
        cbFull.IsChecked = cfg.Fullscreen;
        cbClose.IsChecked = cfg.CloseOnLaunch;
    }

    void Ram_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (txtRam == null) return;
        txtRam.Text = $"ОЗУ: {(int)slRam.Value} МБ";
    }

    void SaveSettings_Click(object s, RoutedEventArgs e)
    {
        cfg.RamMb = (int)slRam.Value;
        cfg.ExtraJvm = tbJvm.Text.Trim();
        cfg.JavaPath = tbJava.Text.Trim();
        cfg.Width = int.TryParse(tbW.Text, out var w) && w >= 320 ? w : 854;
        cfg.Height = int.TryParse(tbH.Text, out var h) && h >= 240 ? h : 480;
        cfg.Fullscreen = cbFull.IsChecked == true;
        cfg.CloseOnLaunch = cbClose.IsChecked == true;
        cfg.Save();
        LoadSettingsUi();
        MessageBox.Show("Настройки сохранены", "AZURE");
    }

    void OpenRoot_Click(object s, RoutedEventArgs e) => OpenFolder(Paths.Root);
    void OpenLogs_Click(object s, RoutedEventArgs e) => OpenFolder(Paths.Logs);

    // ---------- запуск ----------
    void Report(string text, double frac)
    {
        Dispatcher.Invoke(() =>
        {
            txtStatus.Text = text;
            if (frac < 0) pbar.IsIndeterminate = true;
            else { pbar.IsIndeterminate = false; pbar.Value = frac * 100; }
        });
    }

    void Log(string line)
    {
        Dispatcher.InvokeAsync(() =>
        {
            txtLog.AppendText(line + "\n");
            if (txtLog.Text.Length > 150000) txtLog.Text = txtLog.Text[^60000..];
            txtLog.ScrollToEnd();
        });
    }

    async void Play_Click(object s, RoutedEventArgs e)
    {
        if (running) return;
        if (lbPlayInstance.SelectedItem is not Instance inst) { MessageBox.Show("Сначала создай сборку во вкладке «Сборки»", "AZURE"); return; }
        if (lbPlayAccount.SelectedItem is not Account acc) { MessageBox.Show("Сначала добавь аккаунт во вкладке «Аккаунты»", "AZURE"); return; }

        running = true;
        btnPlay.IsEnabled = false;
        txtLog.Clear();

        try
        {
            var res = await GameLauncher.Start(cfg, inst, acc, Report, Log, () =>
            {
                if (cfg.CloseOnLaunch) Dispatcher.Invoke(() => Hide());
            });
            cfg.Save(); // сохраняем ResolvedId загрузчика

            Dispatcher.Invoke(() => { if (!IsVisible) Show(); });
            Report(res.ExitCode == 0 ? "Игра закрыта" : $"Игра завершилась с ошибкой (код {res.ExitCode})", 0);

            if (res.ExitCode != 0)
            {
                string tail; lock (res.Tail) tail = string.Join("\n", res.Tail.TakeLast(120));
                var report = FindCrashReport(res);
                var all = tail + "\n" + (report ?? "");
                var win = new CrashWindow(
                    $"Игра вылетела (код {res.ExitCode})",
                    CrashAnalyzer.Analyze(all, res.ExitCode),
                    (report != null ? "=== crash-report ===\n" + report + "\n\n" : "") + "=== последние строки лога ===\n" + tail,
                    Path.Combine(res.GameDir, "crash-reports"));
                win.Owner = this;
                win.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            cfg.Save();
            if (!IsVisible) Show();
            Report("Ошибка", 0);
            new CrashWindow("Не удалось запустить игру",
                "• Проверь интернет и повтори попытку.\n\n• Если ошибка повторяется — пришли текст ниже разработчику.",
                ex.ToString(), Paths.Logs) { Owner = this }.ShowDialog();
        }
        finally
        {
            running = false;
            btnPlay.IsEnabled = true;
        }
    }

    static string FindCrashReport(RunResult res)
    {
        try
        {
            var dir = Path.Combine(res.GameDir, "crash-reports");
            if (!Directory.Exists(dir)) return null;
            var f = new DirectoryInfo(dir).GetFiles("crash-*.txt")
                .Where(x => x.LastWriteTime >= res.Started.AddMinutes(-1))
                .OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
            if (f == null) return null;
            var t = File.ReadAllText(f.FullName);
            return t.Length > 8000 ? t[..8000] + "\n…(обрезано, полный файл в папке crash-reports)" : t;
        }
        catch { return null; }
    }
}
