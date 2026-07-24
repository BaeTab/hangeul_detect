using System.Diagnostics;
using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Windowing;

/// <summary>포그라운드 창/프로세스 조회(제외 목록·포커스 변경 판정용).</summary>
public static class ForegroundInfo
{
    public static (IntPtr Hwnd, uint Pid) Current()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return (hwnd, pid);
    }

    /// <summary>확장자 없는 프로세스명(소문자). 실패 시 null.</summary>
    public static string? ProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
