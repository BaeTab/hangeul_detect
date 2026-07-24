using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Security;

/// <summary>
/// 비밀번호/보안 입력 컨텍스트를 감지한다. 하나라도 걸리면 수집을 중단해야 한다.
///  1) 포커스 컨트롤의 ES_PASSWORD 스타일 (클래스가 Edit일 때만)
///  2) UI Automation IsPassword (별도 스레드 150ms 타임아웃)
///  3) 프로세스 블랙리스트(암호관리자/보안·은행 SW/사용자 추가)
/// 포커스 창이 바뀌거나 250ms 지나면 재판정한다.
/// </summary>
public sealed class SecureFieldDetector
{
    private const long CacheMs = 250;
    private const int UiaTimeoutMs = 150;
    private const long ES_PASSWORD = NativeMethods.ES_PASSWORD;

    // 기본 블랙리스트(프로세스명 부분일치, 대소문자 무시)
    private static readonly string[] DefaultBlacklist =
    {
        // 비밀번호 관리자
        "keepass", "1password", "bitwarden", "dashlane", "lastpass", "enpass", "keeper", "roboform",
        // 국내 보안/은행 플러그인
        "ahnlab", "aostray", "nprotect", "npkcmsvc", "wizvera", "veraport",
        "touchen", "astx", "delfino", "inisafe", "xecure", "magicline",
    };

    private readonly List<string> _blacklist;

    private IntPtr _lastFocus = IntPtr.Zero;
    private bool _cached;
    private long _cachedAtMs = long.MinValue;

    public SecureFieldDetector(IEnumerable<string>? extraProcesses = null)
    {
        _blacklist = new List<string>(DefaultBlacklist);
        if (extraProcesses != null)
            foreach (var p in extraProcesses)
                if (!string.IsNullOrWhiteSpace(p))
                    _blacklist.Add(p.Trim().ToLowerInvariant());
    }

    public void AddBlacklisted(string processName)
    {
        if (!string.IsNullOrWhiteSpace(processName))
            _blacklist.Add(processName.Trim().ToLowerInvariant());
    }

    /// <summary>지금 포커스가 비밀번호/보안 입력 컨텍스트인가?</summary>
    public bool IsSecureContext()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        uint threadId = NativeMethods.GetWindowThreadProcessId(fg, out uint pid);

        IntPtr focus = fg;
        var gti = new NativeMethods.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (NativeMethods.GetGUIThreadInfo(threadId, ref gti) && gti.hwndFocus != IntPtr.Zero)
            focus = gti.hwndFocus;

        long now = Environment.TickCount64;
        if (focus == _lastFocus && now - _cachedAtMs < CacheMs)
            return _cached;

        _lastFocus = focus;
        _cachedAtMs = now;
        _cached = IsBlacklistedProcess(pid) || HasPasswordStyle(focus) || IsUiaPassword();
        return _cached;
    }

    private bool IsBlacklistedProcess(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string name = p.ProcessName.ToLowerInvariant();
            foreach (var b in _blacklist)
                if (name.Contains(b, StringComparison.Ordinal))
                    return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasPasswordStyle(IntPtr hwndFocus)
    {
        if (hwndFocus == IntPtr.Zero) return false;

        // ES_PASSWORD(0x20)는 Edit 컨트롤에서만 의미가 있다 — 클래스 확인 후 판정.
        var buf = new char[64];
        int len = NativeMethods.GetClassName(hwndFocus, buf, buf.Length);
        if (len <= 0) return false;
        string cls = new string(buf, 0, len);
        if (!cls.Equals("Edit", StringComparison.OrdinalIgnoreCase) &&
            !cls.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            return false;

        long style = NativeMethods.GetWindowLongPtrW(hwndFocus, NativeMethods.GWL_STYLE).ToInt64();
        return (style & ES_PASSWORD) != 0;
    }

    private static bool IsUiaPassword()
    {
        // UIA는 앱에 따라 수 초간 블로킹될 수 있으므로 별도 스레드 + 타임아웃.
        try
        {
            var task = Task.Run(() =>
            {
                var el = AutomationElement.FocusedElement;
                if (el is null) return false;
                var val = el.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty);
                return val is bool b && b;
            });
            return task.Wait(UiaTimeoutMs) && task.Result;
        }
        catch
        {
            return false;
        }
    }
}
