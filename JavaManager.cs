using System.IO.Compression;

namespace Luba.Core;

public static class JavaManager
{
    // 1.16.x -> Java 8 (лучше всего дружит с Forge 1.16.5), 1.17+ -> Java 17
    public static int Required(string mc)
    {
        var p = mc.Split('.');
        int minor = p.Length > 1 && int.TryParse(p[1], out var m) ? m : 20;
        return minor <= 16 ? 8 : 17;
    }

    public static async Task<string> Ensure(int major, Settings s, Action<string, double> rep)
    {
        if (!string.IsNullOrWhiteSpace(s.JavaPath) && File.Exists(s.JavaPath)) return s.JavaPath;

        var dir = Path.Combine(Paths.Runtime, "java" + major);
        string Find() => Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "java.exe", SearchOption.AllDirectories).FirstOrDefault()
            : null;

        var exe = Find();
        if (exe != null) return exe;

        rep($"Загрузка Java {major}…", -1);
        var zip = Path.Combine(Paths.Temp, $"java{major}.zip");
        await Net.Download(
            $"https://api.adoptium.net/v3/binary/latest/{major}/ga/windows/x64/jre/hotspot/normal/eclipse",
            zip, null);
        ZipFile.ExtractToDirectory(zip, dir, true);
        try { File.Delete(zip); } catch { }

        exe = Find();
        if (exe == null) throw new Exception("Не удалось установить Java " + major);
        return exe;
    }
}
