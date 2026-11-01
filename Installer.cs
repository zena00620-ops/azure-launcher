using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Luba.Core;

public static class Rules
{
    // Правила Mojang: по умолчанию запрещено, побеждает последнее подходящее правило
    public static bool Allowed(JsonNode rules)
    {
        if (rules == null) return true;
        bool allowed = false;
        foreach (var r in rules.AsArray())
        {
            bool match = true;
            var os = r["os"];
            if (os != null)
            {
                var n = os["name"]?.GetValue<string>();
                if (n != null && n != "windows") match = false;
                var a = os["arch"]?.GetValue<string>();
                if (a == "x86") match = false;
            }
            if (r["features"] != null) match = false;
            if (match) allowed = r["action"].GetValue<string>() == "allow";
        }
        return allowed;
    }

    public static bool SkipArch(JsonNode lib)
    {
        var n = lib["name"].GetValue<string>();
        return n.EndsWith(":natives-windows-x86") || n.EndsWith(":natives-windows-arm64");
    }
}

public class VersionInfo
{
    public string Id, MainClass, AssetsId, AssetIndexUrl, AssetIndexSha1, ClientJar;
    public List<string> Game = new(), Jvm = new();
    public List<JsonNode> Libs = new();

    static string Key(string name)
    {
        var at = name.IndexOf('@');
        if (at >= 0) name = name[..at];
        var p = name.Split(':');
        return p.Length > 3 ? $"{p[0]}:{p[1]}:{p[3]}" : $"{p[0]}:{p[1]}";
    }

    public static VersionInfo Load(string id)
    {
        var j = JsonNode.Parse(File.ReadAllText(Paths.VersionJson(id)));
        var parent = j["inheritsFrom"]?.GetValue<string>();
        var v = parent != null ? Load(parent) : new VersionInfo { ClientJar = Paths.VersionJar(id) };
        v.Id = id;

        if (j["mainClass"] != null) v.MainClass = j["mainClass"].GetValue<string>();

        var ai = j["assetIndex"];
        if (ai != null)
        {
            v.AssetsId = ai["id"].GetValue<string>();
            v.AssetIndexUrl = ai["url"].GetValue<string>();
            v.AssetIndexSha1 = ai["sha1"]?.GetValue<string>();
        }

        if (j["libraries"] is JsonArray libs)
        {
            foreach (var l in libs)
            {
                var key = Key(l["name"].GetValue<string>());
                v.Libs.RemoveAll(x => Key(x["name"].GetValue<string>()) == key);
                v.Libs.Add(l);
            }
        }

        AddArgs(j["arguments"]?["game"], v.Game);
        AddArgs(j["arguments"]?["jvm"], v.Jvm);
        if (j["minecraftArguments"] != null)
            v.Game.AddRange(j["minecraftArguments"].GetValue<string>().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return v;
    }

    static void AddArgs(JsonNode arr, List<string> into)
    {
        if (arr == null) return;
        foreach (var a in arr.AsArray())
        {
            if (a is JsonValue) { into.Add(a.GetValue<string>()); continue; }
            if (!Rules.Allowed(a["rules"])) continue;
            var val = a["value"];
            if (val is JsonArray ar) foreach (var s in ar) into.Add(s.GetValue<string>());
            else if (val != null) into.Add(val.GetValue<string>());
        }
    }

    public static string MavenPath(string name)
    {
        string ext = "jar";
        var at = name.IndexOf('@');
        if (at >= 0) { ext = name[(at + 1)..]; name = name[..at]; }
        var p = name.Split(':');
        var g = p[0].Replace('.', '/');
        var cl = p.Length > 3 ? "-" + p[3] : "";
        return $"{g}/{p[1]}/{p[2]}/{p[1]}-{p[2]}{cl}.{ext}";
    }

    // Относительный путь jar в libraries/ (null — в classpath не идёт)
    public static string LibRelPath(JsonNode lib)
    {
        var art = lib["downloads"]?["artifact"];
        if (art != null && art["path"] != null) return art["path"].GetValue<string>();
        if (lib["natives"] != null) return null;
        return MavenPath(lib["name"].GetValue<string>());
    }

    public static (string url, string sha1) LibSource(JsonNode lib, string rel)
    {
        var art = lib["downloads"]?["artifact"];
        if (art != null)
        {
            var u = art["url"]?.GetValue<string>();
            if (string.IsNullOrEmpty(u)) return (null, null); // файл создаёт установщик Forge
            return (u, art["sha1"]?.GetValue<string>());
        }
        var baseUrl = lib["url"]?.GetValue<string>() ?? "https://libraries.minecraft.net/";
        return (baseUrl.TrimEnd('/') + "/" + rel, null);
    }

    public static string NativeClassifier(JsonNode lib)
        => lib["natives"]?["windows"]?.GetValue<string>()?.Replace("${arch}", "64");

    public static string LibFile(string rel) => Path.Combine(Paths.Libraries, rel.Replace('/', Path.DirectorySeparatorChar));
}

public static class Installer
{
    public static async Task<string> EnsureInstalled(Settings s, Instance inst, Action<string, double> rep, Action<string> log)
    {
        var mc = inst.McVersion;
        await PrepareVanilla(mc, rep);
        if (inst.Loader == "Vanilla") return mc;

        if (!string.IsNullOrEmpty(inst.ResolvedId) && File.Exists(Paths.VersionJson(inst.ResolvedId)))
            return inst.ResolvedId;

        var id = inst.Loader == "Fabric"
            ? await InstallFabric(mc, rep)
            : await InstallForge(s, mc, rep, log);
        inst.ResolvedId = id;
        return id;
    }

    public static async Task PrepareVanilla(string mc, Action<string, double> rep)
    {
        var jsonPath = Paths.VersionJson(mc);
        if (!File.Exists(jsonPath))
        {
            rep($"Получение данных {mc}…", -1);
            var man = JsonNode.Parse(await Net.GetString("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"));
            var entry = man["versions"].AsArray().FirstOrDefault(x => x["id"].GetValue<string>() == mc);
            if (entry == null) throw new Exception($"Версия {mc} не найдена в манифесте Mojang");
            await Net.Download(entry["url"].GetValue<string>(), jsonPath, null);
        }
        var j = JsonNode.Parse(File.ReadAllText(jsonPath));
        var c = j["downloads"]["client"];
        rep($"Клиент Minecraft {mc}…", -1);
        await Net.Download(c["url"].GetValue<string>(), Paths.VersionJar(mc), c["sha1"]?.GetValue<string>());
    }

    static async Task<string> InstallFabric(string mc, Action<string, double> rep)
    {
        rep("Установка Fabric…", -1);
        var arr = JsonNode.Parse(await Net.GetString($"https://meta.fabricmc.net/v2/versions/loader/{mc}")).AsArray();
        var entry = arr.FirstOrDefault(x => x["loader"]["stable"].GetValue<bool>()) ?? arr.FirstOrDefault();
        if (entry == null) throw new Exception($"Для {mc} нет Fabric");
        var lv = entry["loader"]["version"].GetValue<string>();
        var id = $"fabric-loader-{lv}-{mc}";
        if (!File.Exists(Paths.VersionJson(id)))
        {
            var json = await Net.GetString($"https://meta.fabricmc.net/v2/versions/loader/{mc}/{lv}/profile/json");
            Directory.CreateDirectory(Paths.VersionDir(id));
            File.WriteAllText(Paths.VersionJson(id), json);
        }
        return id;
    }

    static async Task<string> InstallForge(Settings s, string mc, Action<string, double> rep, Action<string> log)
    {
        rep("Поиск версии Forge…", -1);
        var promos = JsonNode.Parse(await Net.GetString("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json"))["promos"];
        var fv = promos[$"{mc}-recommended"]?.GetValue<string>() ?? promos[$"{mc}-latest"]?.GetValue<string>();
        if (fv == null) throw new Exception($"Для {mc} нет Forge");

        var expected = $"{mc}-forge-{fv}";
        if (File.Exists(Paths.VersionJson(expected))) return expected;

        rep($"Загрузка установщика Forge {fv}…", -1);
        var installer = Path.Combine(Paths.Temp, $"forge-{mc}-{fv}-installer.jar");
        await Net.Download($"https://maven.minecraftforge.net/net/minecraftforge/forge/{mc}-{fv}/forge-{mc}-{fv}-installer.jar", installer, null);

        var java = await JavaManager.Ensure(JavaManager.Required(mc), s, rep);
        var prof = Path.Combine(Paths.Root, "launcher_profiles.json");
        if (!File.Exists(prof)) File.WriteAllText(prof, "{\"profiles\":{}}");

        rep("Установка Forge (это может занять пару минут)…", -1);
        var psi = new ProcessStartInfo(java)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Paths.Root
        };
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(installer);
        psi.ArgumentList.Add("--installClient");
        psi.ArgumentList.Add(Paths.Root);

        var lines = new List<string>();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, a) => { if (a.Data != null) { lock (lines) lines.Add(a.Data); log(a.Data); } };
        p.ErrorDataReceived += (_, a) => { if (a.Data != null) { lock (lines) lines.Add(a.Data); log(a.Data); } };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await Task.Run(() => p.WaitForExit());

        if (p.ExitCode != 0)
        {
            string tail; lock (lines) tail = string.Join("\n", lines.TakeLast(15));
            throw new Exception("Установщик Forge завершился с ошибкой:\n" + tail);
        }

        if (File.Exists(Paths.VersionJson(expected))) return expected;
        var found = Directory.GetDirectories(Paths.Versions).Select(d => Path.GetFileName(d))
            .FirstOrDefault(n => n.Contains("forge", StringComparison.OrdinalIgnoreCase) && n.Contains(fv));
        if (found == null) throw new Exception("Forge установился, но версия не найдена в папке versions");
        return found;
    }

    static async Task ParallelDownload(List<(string url, string path, string sha1)> items, Action<string, double> rep, string label)
    {
        if (items.Count == 0) return;
        int done = 0;
        var sem = new SemaphoreSlim(12);
        var tasks = items.Select(async it =>
        {
            await sem.WaitAsync();
            try { await Net.Download(it.url, it.path, it.sha1); }
            finally
            {
                sem.Release();
                var d = Interlocked.Increment(ref done);
                if (d % 20 == 0 || d == items.Count) rep($"{label} {d}/{items.Count}", (double)d / items.Count);
            }
        }).ToList();
        await Task.WhenAll(tasks);
    }

    public static async Task DownloadLibs(VersionInfo v, Action<string, double> rep)
    {
        var items = new List<(string url, string path, string sha1)>();
        foreach (var lib in v.Libs)
        {
            if (!Rules.Allowed(lib["rules"]) || Rules.SkipArch(lib)) continue;

            var rel = VersionInfo.LibRelPath(lib);
            if (rel != null)
            {
                var (u, s) = VersionInfo.LibSource(lib, rel);
                if (u != null) items.Add((u, VersionInfo.LibFile(rel), s));
            }

            var cls = VersionInfo.NativeClassifier(lib);
            if (cls != null)
            {
                var c = lib["downloads"]?["classifiers"]?[cls];
                if (c != null)
                    items.Add((c["url"].GetValue<string>(), VersionInfo.LibFile(c["path"].GetValue<string>()), c["sha1"]?.GetValue<string>()));
            }
        }
        await ParallelDownload(items, rep, "Библиотеки");
    }

    public static async Task DownloadAssets(VersionInfo v, Action<string, double> rep)
    {
        var idx = Path.Combine(Paths.Assets, "indexes", v.AssetsId + ".json");
        await Net.Download(v.AssetIndexUrl, idx, v.AssetIndexSha1);
        var j = JsonNode.Parse(File.ReadAllText(idx));
        var items = new List<(string url, string path, string sha1)>();
        foreach (var kv in j["objects"].AsObject())
        {
            var h = kv.Value["hash"].GetValue<string>();
            var pre = h[..2];
            items.Add(($"https://resources.download.minecraft.net/{pre}/{h}", Path.Combine(Paths.Assets, "objects", pre, h), null));
        }
        await ParallelDownload(items, rep, "Ресурсы");
    }

    // Лёгкая оптимизация: проверенные моды с Modrinth (не патч кода Minecraft, а отдельные моды)
    const string Marker = "azure-opt-";

    public static async Task SyncOptimizationMods(Instance inst, string modsDir, Action<string> log)
    {
        Directory.CreateDirectory(modsDir);

        if (!inst.Optimize)
        {
            foreach (var f in Directory.GetFiles(modsDir, Marker + "*.jar")) { try { File.Delete(f); } catch { } }
            return;
        }

        var slugs = inst.Loader == "Fabric"
            ? new[] { "sodium", "lithium", "ferrite-core", "modernfix" }
            : new[] { "embeddium", "ferrite-core", "modernfix" };

        foreach (var slug in slugs)
        {
            try
            {
                var url = $"https://api.modrinth.com/v2/project/{slug}/version" +
                          $"?game_versions={Uri.EscapeDataString($"[\"{inst.McVersion}\"]")}" +
                          $"&loaders={Uri.EscapeDataString($"[\"{inst.Loader.ToLowerInvariant()}\"]")}";
                var arr = JsonNode.Parse(await Net.GetString(url)).AsArray();
                if (arr.Count == 0) { log($"[оптимизация] {slug}: нет версии для {inst.McVersion}, пропуск"); continue; }

                var files = arr[0]["files"].AsArray();
                var f = files.FirstOrDefault(x => x["primary"]?.GetValue<bool>() == true) ?? files[0];
                var name = Marker + slug + "__" + f["filename"].GetValue<string>();
                var target = Path.Combine(modsDir, name);
                if (File.Exists(target)) continue;

                foreach (var old in Directory.GetFiles(modsDir, Marker + slug + "__*.jar")) { try { File.Delete(old); } catch { } }
                await Net.Download(f["url"].GetValue<string>(), target, null);
                log($"[оптимизация] установлен {slug}");
            }
            catch (Exception ex)
            {
                log($"[оптимизация] {slug}: пропущен ({ex.Message})");
            }
        }
    }
}
