using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Luba.Core;

public class Account
{
    public string Name { get; set; } = "";
    public string Uuid { get; set; } = "";

    public static Account Create(string name) => new Account { Name = name, Uuid = OfflineUuid(name) };

    // Тот же алгоритм, что у Java UUID.nameUUIDFromBytes("OfflinePlayer:" + name)
    public static string OfflineUuid(string name)
    {
        var h = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        h[6] = (byte)((h[6] & 0x0f) | 0x30);
        h[8] = (byte)((h[8] & 0x3f) | 0x80);
        var x = Convert.ToHexString(h).ToLowerInvariant();
        return $"{x[..8]}-{x.Substring(8, 4)}-{x.Substring(12, 4)}-{x.Substring(16, 4)}-{x.Substring(20)}";
    }

    public override string ToString() => Name;
}

public class Instance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Сборка";
    public string McVersion { get; set; } = "1.20.1";
    public string Loader { get; set; } = "Fabric"; // Vanilla | Fabric | Forge
    public bool Optimize { get; set; } = true;
    public string ResolvedId { get; set; } = "";   // id версии в versions/ после установки загрузчика

    public override string ToString() => $"{Name}  ·  {McVersion} {Loader}";
}

public class Settings
{
    public List<Account> Accounts { get; set; } = new();
    public List<Instance> Instances { get; set; } = new();
    public string SelectedInstance { get; set; } = "";
    public string SelectedAccount { get; set; } = "";
    public int RamMb { get; set; } = DefaultRam();
    public string ExtraJvm { get; set; } = "";
    public string JavaPath { get; set; } = "";
    public string Accent { get; set; } = "#2DA8FF";
    public bool CloseOnLaunch { get; set; } = false;
    public bool Fullscreen { get; set; } = false;
    public int Width { get; set; } = 854;
    public int Height { get; set; } = 480;

    public static int TotalMb() => (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1048576);

    static int DefaultRam() => Math.Min(2048, Math.Max(1024, TotalMb() / 2 / 256 * 256));

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(Paths.Config))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Paths.Config));
                if (s != null) return s;
            }
        }
        catch { /* битый конфиг — начинаем заново, старый сохраняем */
            try { File.Move(Paths.Config, Paths.Config + ".bad", true); } catch { }
        }

        var n = new Settings();
        var acc = Account.Create("Player");
        n.Accounts.Add(acc);
        n.SelectedAccount = acc.Name;
        var inst = new Instance { Name = "Моя сборка", McVersion = "1.20.1", Loader = "Fabric" };
        n.Instances.Add(inst);
        n.SelectedInstance = inst.Id;
        n.Save();
        return n;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.Root);
            File.WriteAllText(Paths.Config, JsonSerializer.Serialize(this, Opt));
        }
        catch { }
    }
}
