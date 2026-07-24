using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Windowing;

/// <summary>
/// 오버레이 창을 "포커스 안 뺏고 클릭 통과"로 만드는 인터롭 헬퍼.
/// 포커스를 뺏으면 사용자의 IME 조합이 끊긴다 — 이 제약이 최우선이다.
/// </summary>
public static class OverlayInterop
{
    /// <summary>확장 스타일에 WS_EX_NOACTIVATE | TRANSPARENT | TOOLWINDOW를 추가한다.</summary>
    public static void MakeClickThroughNoActivate(IntPtr hwnd)
    {
        long ex = NativeMethods.GetWindowLongPtrW(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtrW(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    /// <summary>물리 픽셀 좌표로 이동(크기·활성화 변경 없음). 최상위 유지.</summary>
    public static void MoveNoActivate(IntPtr hwnd, int x, int y)
    {
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
