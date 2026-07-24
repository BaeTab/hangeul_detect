using System.Runtime.InteropServices;
using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Ime;

/// <summary>
/// 현재 포그라운드 창이 한글 입력 모드인지 판정한다.
/// LANGID(ko-KR 0x0412) + IME 변환 모드(IME_CMODE_NATIVE)를 확인하고 200ms 캐시한다.
/// </summary>
public sealed class ImeStateReader
{
    private const long CacheMs = 200;

    private bool _cached;
    private long _cachedAtMs = long.MinValue;

    /// <summary>한글(NATIVE) 입력 모드면 true. 영문/비한국어 레이아웃이면 false.</summary>
    public bool IsHangulMode()
    {
        long now = Environment.TickCount64;
        if (now - _cachedAtMs < CacheMs) return _cached;
        _cached = Query();
        _cachedAtMs = now;
        return _cached;
    }

    private static bool Query()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        uint threadId = NativeMethods.GetWindowThreadProcessId(fg, out _);

        // 1) 키보드 레이아웃 하위 워드(LANGID)가 한국어인지
        IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);
        uint langId = (uint)(hkl.ToInt64() & 0xFFFF);
        if (langId != NativeMethods.KLF_KOREAN_LANGID) return false;

        // 포커스 컨트롤(있으면)의 IME 컨텍스트를 우선 사용
        IntPtr target = fg;
        var gti = new NativeMethods.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (NativeMethods.GetGUIThreadInfo(threadId, ref gti) && gti.hwndFocus != IntPtr.Zero)
            target = gti.hwndFocus;

        // 2) IME 변환 모드가 NATIVE(한글)인지
        IntPtr himc = NativeMethods.ImmGetContext(target);
        if (himc == IntPtr.Zero)
            return false;   // 한국어 레이아웃이나 조합 상태 불명 → 보수적으로 영문 취급

        try
        {
            if (NativeMethods.ImmGetConversionStatus(himc, out int conv, out _))
                return (conv & NativeMethods.IME_CMODE_NATIVE) != 0;
            return false;
        }
        finally
        {
            NativeMethods.ImmReleaseContext(target, himc);
        }
    }
}
