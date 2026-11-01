namespace Luba.Core;

public static class Paths
{
    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".azure");

    public static string Versions => Path.Combine(Root, "versions");
    public static string Libraries => Path.Combine(Root, "libraries");
    public static string Assets => Path.Combine(Root, "assets");
    public static string Runtime => Path.Combine(Root, "runtime");
    public static string Instances => Path.Combine(Root, "instances");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Temp => Path.Combine(Root, "temp");
    public static string Config => Path.Combine(Root, "launcher.json");

    public static string VersionDir(string id) => Path.Combine(Versions, id);
    public static string VersionJson(string id) => Path.Combine(Versions, id, id + ".json");
    public static string VersionJar(string id) => Path.Combine(Versions, id, id + ".jar");

    public static void EnsureAll()
    {
        foreach (var d in new[] { Root, Versions, Libraries, Assets, Runtime, Instances, Logs, Temp })
            Directory.CreateDirectory(d);
    }
}
