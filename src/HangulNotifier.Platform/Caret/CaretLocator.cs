using System.Runtime.InteropServices;
using System.Windows.Automation;
using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Caret;

/// <summary>캐럿(또는 대체) 위치. 물리 픽셀 좌표. Found=false면 폴백 위치.</summary>
public readonly record struct CaretLocation(double X, double Y, double Height, bool Found);

/// <summary>
/// 캐럿 화면 좌표를 3단계 폴백으로 구한다(물리 픽셀).
///  1) GetGUIThreadInfo → rcCaret → ClientToScreen
///  2) UI Automation TextPattern (별도 스레드 150ms 타임아웃) — Chrome/Electron 대응
///  3) 마우스 커서 기준, 그마저 없으면 작업표시줄 위 우하단 고정
/// 멀티 모니터에서 화면 밖으로 나가지 않도록 해당 모니터 작업영역으로 클램프한다.
/// </summary>
public sealed class CaretLocator
{
    private const int UiaTimeoutMs = 150;
    private const double DefaultCaretHeight = 18;

    public CaretLocation Locate()
    {
        var loc = TryGuiThreadInfo() ?? TryUia() ?? Fallback();
        return Clamp(loc);
    }

    // 1) GetGUIThreadInfo
    private static CaretLocation? TryGuiThreadInfo()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return null;
        uint threadId = NativeMethods.GetWindowThreadProcessId(fg, out _);

        var gti = new NativeMethods.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(threadId, ref gti)) return null;
        if (gti.hwndCaret == IntPtr.Zero || gti.rcCaret.IsEmpty) return null;

        var pt = new NativeMethods.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
        if (!NativeMethods.ClientToScreen(gti.hwndCaret, ref pt)) return null;

        double height = Math.Max(gti.rcCaret.Bottom - gti.rcCaret.Top, DefaultCaretHeight);
        return new CaretLocation(pt.X, pt.Y, height, Found: true);
    }

    // 2) UI Automation TextPattern (타임아웃)
    private static CaretLocation? TryUia()
    {
        try
        {
            var task = Task.Run(() =>
            {
                var el = AutomationElement.FocusedElement;
                if (el is null) return (CaretLocation?)null;
                if (!el.TryGetCurrentPattern(TextPattern.Pattern, out object pat)) return null;
                var tp = (TextPattern)pat;
                var sel = tp.GetSelection();
                if (sel is null || sel.Length == 0) return null;
                var rects = sel[0].GetBoundingRectangles();
                if (rects is null || rects.Length == 0) return null;
                var r = rects[0];
                return new CaretLocation(r.Left, r.Bottom, r.Height > 0 ? r.Height : DefaultCaretHeight, true);
            });
            if (task.Wait(UiaTimeoutMs)) return task.Result;
            return null;
        }
        catch
        {
            return null;
        }
    }

    // 3) 마우스 커서 / 우하단 고정
    private static CaretLocation Fallback()
    {
        if (NativeMethods.GetCursorPos(out var p))
            return new CaretLocation(p.X + 12, p.Y + 20, DefaultCaretHeight, Found: false);

        // 커서도 못 얻으면 주 모니터 우하단
        var mi = MonitorInfoAt(new NativeMethods.POINT { X = 0, Y = 0 });
        return new CaretLocation(mi.rcWork.Right - 320, mi.rcWork.Bottom - 100, DefaultCaretHeight, Found: false);
    }

    private static CaretLocation Clamp(CaretLocation loc)
    {
        var mi = MonitorInfoAt(new NativeMethods.POINT { X = (int)loc.X, Y = (int)loc.Y });
        var work = mi.rcWork;
        const double overlayW = 320, overlayH = 90;

        double x = Math.Min(Math.Max(loc.X, work.Left), work.Right - overlayW);
        double y = Math.Min(Math.Max(loc.Y, work.Top), work.Bottom - overlayH);
        return loc with { X = x, Y = y };
    }

    private static NativeMethods.MONITORINFO MonitorInfoAt(NativeMethods.POINT pt)
    {
        IntPtr hmon = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(hmon, ref mi);
        return mi;
    }
}
