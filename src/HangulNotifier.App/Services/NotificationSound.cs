using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Serilog;

namespace HangulNotifier.App.Services;

/// <summary>
/// 알림음 재생.
///
/// <para><see cref="System.Media.SystemSounds"/>는 볼륨 인자를 받지 않아 사용자가 설정한 볼륨이
/// 그대로 무시된다. 그래서 시스템 알림 WAV 파일을 <see cref="MediaPlayer"/>로 직접 재생해
/// 볼륨을 실제로 반영한다.</para>
///
/// <para>WAV를 찾지 못하거나(레지스트리 미설정·파일 없음) 미디어 코덱이 없는 환경(Windows N 등)에서는
/// SystemSounds로 폴백한다. 폴백 상태에서는 볼륨이 적용되지 않으며, 그 사실을 로그로 한 번 남긴다.</para>
///
/// <para>워커 스레드에서 호출되므로 UI 스레드로 마샬링한다(MediaPlayer는 DispatcherObject).</para>
/// </summary>
public sealed class NotificationSound
{
    // 시스템 '알림' 이벤트에 연결된 WAV 경로가 담긴 레지스트리 위치.
    private const string NotificationKey = @"AppEvents\Schemes\Apps\.Default\Notification.Default\.Current";
    private const string DefaultBeepKey = @"AppEvents\Schemes\Apps\.Default\.Default\.Current";

    private readonly Dispatcher _ui;
    private readonly ILogger _log;
    private readonly string? _wavPath;

    private MediaPlayer? _player;
    private bool _opened;
    private bool _degraded;   // 재생 실패 → 이후 SystemSounds로만 재생

    public NotificationSound(Dispatcher ui, ILogger log)
    {
        _ui = ui;
        _log = log;
        _wavPath = ResolveWavPath();

        if (_wavPath is null)
        {
            _degraded = true;
            _log.Information("알림음 WAV를 찾지 못해 시스템 기본음으로 재생합니다 (볼륨 조절 미적용)");
        }
    }

    /// <summary>볼륨 조절이 실제로 적용되는 상태인지. 설정 화면에서 안내 문구에 사용.</summary>
    public bool VolumeSupported => !_degraded;

    /// <summary>
    /// 알림음을 재생한다. 볼륨은 0.0~1.0으로 클램프된다.
    /// 어떤 실패도 호출자(감지 파이프라인)로 전파하지 않는다 — 소리 때문에 감지가 멈추면 안 된다.
    /// </summary>
    public void Play(double volume)
    {
        double v = Math.Clamp(volume, 0.0, 1.0);

        if (_degraded)
        {
            PlaySystemSound();
            return;
        }

        // 볼륨 0은 '무음'이라는 사용자 의사이므로 재생 자체를 건너뛴다.
        if (v <= 0.0) return;

        try { _ui.InvokeAsync(() => PlayOnUi(v)); }
        catch (Exception ex)
        {
            _log.Warning(ex, "알림음 마샬링 실패 — 시스템 기본음으로 전환합니다");
            Degrade();
            PlaySystemSound();
        }
    }

    private void PlayOnUi(double volume)
    {
        try
        {
            if (_player is null)
            {
                var player = new MediaPlayer();
                player.MediaOpened += (_, _) => _opened = true;
                player.MediaFailed += (_, e) =>
                {
                    _log.Warning(e.ErrorException, "알림음 열기 실패 — 시스템 기본음으로 전환합니다 (볼륨 조절 미적용)");
                    Degrade();
                };
                player.Open(new Uri(_wavPath!, UriKind.Absolute));
                _player = player;
            }

            _player.Volume = volume;

            // 이미 한 번 재생된 미디어는 끝 위치에 멈춰 있으므로 되감아야 다시 들린다.
            // 열리기 전(_opened=false)에 Position을 건드리면 예외가 날 수 있어 열린 뒤에만 되감는다.
            if (_opened) _player.Position = TimeSpan.Zero;

            _player.Play();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "알림음 재생 실패 — 시스템 기본음으로 전환합니다");
            Degrade();
            PlaySystemSound();
        }
    }

    private void Degrade()
    {
        _degraded = true;
        try { _player?.Close(); } catch { /* 정리 실패 무시 */ }
        _player = null;
    }

    private void PlaySystemSound()
    {
        try { System.Media.SystemSounds.Asterisk.Play(); } catch { /* 무음 실패 무시 */ }
    }

    /// <summary>
    /// 시스템에 설정된 알림음 WAV의 실제 경로를 찾는다.
    /// 레지스트리 → 기본 비프 → 잘 알려진 미디어 파일 순으로 시도하고, 모두 실패하면 null.
    /// </summary>
    private static string? ResolveWavPath()
    {
        foreach (var key in new[] { NotificationKey, DefaultBeepKey })
        {
            var path = ReadRegistryPath(key);
            if (path is not null) return path;
        }

        // 사용자가 시스템 소리를 '없음'으로 꺼둔 경우 레지스트리 값이 비어 있다.
        // 앱 알림음은 별개 설정이므로 표준 미디어 파일로 대체한다.
        string media = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

        foreach (var name in new[]
        {
            "Windows Notify System Generic.wav",
            "Windows Notify.wav",
            "Windows Ding.wav",
        })
        {
            string candidate = Path.Combine(media, name);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static string? ReadRegistryPath(string subKey)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            if (key?.GetValue(null) is not string raw || string.IsNullOrWhiteSpace(raw)) return null;

            string expanded = Environment.ExpandEnvironmentVariables(raw.Trim());

            // 상대 경로로 적힌 값(예: "ding.wav")은 %WINDIR%\Media 기준이다.
            if (!Path.IsPathRooted(expanded))
                expanded = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", expanded);

            return File.Exists(expanded) ? expanded : null;
        }
        catch
        {
            return null;   // 레지스트리 접근 실패는 폴백으로 처리
        }
    }
}
