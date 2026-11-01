using System.Security.Cryptography;

namespace Luba.Core;

public static class Net
{
    public static readonly HttpClient Http = Create();

    static HttpClient Create()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("AZURE-Launcher/1.0");
        return h;
    }

    public static Task<string> GetString(string url) => Http.GetStringAsync(url);

    public static string Sha1(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(fs)).ToLowerInvariant();
    }

    public static async Task Download(string url, string path, string sha1)
    {
        if (File.Exists(path))
        {
            if (sha1 == null) return;
            if (string.Equals(Sha1(path), sha1, StringComparison.OrdinalIgnoreCase)) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var tmp = path + ".part";

        for (int i = 0; ; i++)
        {
            try
            {
                using var r = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                r.EnsureSuccessStatusCode();
                using (var fs = File.Create(tmp))
                    await r.Content.CopyToAsync(fs);
                File.Move(tmp, path, true);
                return;
            }
            catch when (i < 2)
            {
                await Task.Delay(1500);
            }
        }
    }
}
