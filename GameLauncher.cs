using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Luba.Core;

public class RunResult
{
    public int ExitCode;
    public List<string> Tail = new();
    public string GameDir;
    public DateTime Started;
}

public static class GameLauncher
{
    public static async Task<RunResult> Start(Settings s, Instance inst, Account acc,
        Action<string, double> rep, Action<string> log, Action onStarted)
    {
        Paths.EnsureAll();
        var gameDir = Path.Combine(Paths.Instances, inst.Id);
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        var id = await Installer.EnsureInstalled(s, inst, rep, log);
        var v = VersionInfo.Load(id);
        await Installer.DownloadLibs(v, rep);
        await Installer.DownloadAssets(v, rep);

        if (inst.Loader != "Vanilla")
        {
            rep("Проверка модов оптимизации…", -1);
            await Installer.SyncOptimizationMods(inst, modsDir, log);
        }

        var jv = JavaManager.Required(inst.McVersion);
        var java = await JavaManager.Ensure(jv, s, rep);

        rep("Подготовка к запуску…", -1);
        var natives = ExtractNatives(v, id);

        // classpath
        var cp = new List<string>();
        foreach (var lib in v.Libs)
        {
            if (!Rules.Allowed(lib["rules"]) || Rules.SkipArch(lib)) continue;
            var rel = VersionInfo.LibRelPath(lib);
            if (rel == null) continue;
            var f = VersionInfo.LibFile(rel);
            if (File.Exists(f) && !cp.Contains(f)) cp.Add(f);
        }
        cp.Add(v.ClientJar);
        var cpStr = string.Join(";", cp);

        var map = new Dictionary<string, string>
        {
            ["auth_player_name"] = acc.Name,
            ["version_name"] = id,
            ["game_directory"] = gameDir,
            ["assets_root"] = Paths.Assets,
            ["game_assets"] = Paths.Assets,
            ["assets_index_name"] = v.AssetsId,
            ["auth_uuid"] = acc.Uuid.Replace("-", ""),
            ["auth_access_token"] = "0",
            ["clientid"] = "0",
            ["auth_xuid"] = "0",
            ["user_type"] = "legacy",
            ["version_type"] = "AZURE",
            ["user_properties"] = "{}",
            ["natives_directory"] = natives,
            ["launcher_name"] = "AZURE",
            ["launcher_version"] = "1.0",
            ["classpath"] = cpStr,
            ["classpath_separator"] = ";",
            ["library_directory"] = Paths.Libraries,
        };
        string Sub(string a)
        {
            foreach (var kv in map) a = a.Replace("${" + kv.Key + "}", kv.Value);
            return a;
        }

        var psi = new ProcessStartInfo(java)
        {
            WorkingDirectory = gameDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        void A(string x) => psi.ArgumentList.Add(x);

        int ram = Math.Max(1024, s.RamMb);
        A($"-Xmx{ram}M");
        A($"-Xms{Math.Min(ram, 1024)}M");
        foreach (var f in new[]
        {
            "-XX:+UnlockExperimentalVMOptions", "-XX:+UseG1GC", "-XX:MaxGCPauseMillis=100",
            "-XX:+DisableExplicitGC", "-XX:+ParallelRefProcEnabled",
            "-XX:G1NewSizePercent=20", "-XX:G1MaxNewSizePercent=40",
            "-Dlog4j2.formatMsgNoLookups=true"
        }) A(f);
        foreach (var x in SplitArgs(s.ExtraJvm)) A(x);

        foreach (var a in v.Jvm) A(Sub(a));
        if (!v.Jvm.Any(a => a.Contains("${classpath}")))
        {
            A("-Djava.library.path=" + natives);
            A("-cp");
            A(cpStr);
        }

        A(v.MainClass);
        foreach (var a in v.Game) A(Sub(a));
        if (s.Fullscreen) A("--fullscreen");
        else { A("--width"); A(s.Width.ToString()); A("--height"); A(s.Height.ToString()); }

        var res = new RunResult { GameDir = gameDir, Started = DateTime.Now };
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        void Line(string d)
        {
            if (d == null) return;
            lock (res.Tail) { res.Tail.Add(d); if (res.Tail.Count > 500) res.Tail.RemoveAt(0); }
            log(d);
        }
        p.OutputDataReceived += (_, a) => Line(a.Data);
        p.ErrorDataReceived += (_, a) => Line(a.Data);

        rep("Игра запущена", 1);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        onStarted?.Invoke();

        await Task.Run(() => p.WaitForExit());
        res.ExitCode = p.ExitCode;

        try
        {
            Directory.CreateDirectory(Paths.Logs);
            lock (res.Tail) File.WriteAllLines(Path.Combine(Paths.Logs, "last-game.log"), res.Tail);
        }
        catch { }
        return res;
    }

    static IEnumerable<string> SplitArgs(string s)
        => string.IsNullOrWhiteSpace(s) ? Array.Empty<string>() : s.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    static string ExtractNatives(VersionInfo v, string id)
    {
        var dir = Path.Combine(Paths.VersionDir(id), "natives");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);

        foreach (var lib in v.Libs)
        {
            if (!Rules.Allowed(lib["rules"]) || Rules.SkipArch(lib)) continue;

            var cls = VersionInfo.NativeClassifier(lib);
            if (cls != null)
            {
                var c = lib["downloads"]?["classifiers"]?[cls];
                if (c != null) ExtractDlls(VersionInfo.LibFile(c["path"].GetValue<string>()), dir);
            }
            else if (lib["name"].GetValue<string>().EndsWith(":natives-windows"))
            {
                var rel = VersionInfo.LibRelPath(lib);
                if (rel != null) ExtractDlls(VersionInfo.LibFile(rel), dir);
            }
        }
        return dir;
    }

    static void ExtractDlls(string jar, string dest)
    {
        if (!File.Exists(jar)) return;
        using var z = ZipFile.OpenRead(jar);
        foreach (var e in z.Entries)
        {
            if (e.FullName.StartsWith("META-INF") || !e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            try { e.ExtractToFile(Path.Combine(dest, e.Name), true); } catch { }
        }
    }
}
