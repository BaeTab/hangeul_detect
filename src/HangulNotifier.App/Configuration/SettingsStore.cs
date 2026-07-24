using System.Text.Json;

namespace HangulNotifier.App.Configuration;

/// <summary>설정 로드/저장. JSON 파일이 없거나 깨졌으면 기본값을 쓴다.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, Opts) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            AppPaths.EnsureRoot();
            var json = JsonSerializer.Serialize(settings, Opts);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // 설정 저장 실패가 앱을 죽이지 않게 한다(로깅은 호출자 책임).
        }
    }
}
