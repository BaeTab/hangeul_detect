using System.Runtime.InteropServices;
using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Ime;

/// <summary>
/// 포그라운드 앱의 한/영 상태를 "가능하면" 읽는다.
///
/// 교차 프로세스 한계: 레거시 IMM32(GetKeyboardLayout/ImmGetContext/ImmGetDefaultIMEWnd)는
/// 자기 프로세스 창에서만 신뢰할 수 있다. 최신 TSF 기반 앱(크롬/카톡/UWP/VS)에서는 다른
/// 프로세스에서 IME 상태를 안전하게 읽을 표준 방법이 없다(AttachThreadInput 은 IME 조합을
/// 방해할 수 있어 이 앱의 절대 원칙상 금지). 따라서 여기서는 기본 IME 창에 WM_IME_CONTROL 을
/// 질의해 "확답을 얻을 수 있으면" 그 값을 돌려주고(클래식 앱), 확답이 없으면 미확정으로 보고한다.
/// 미확정 구간의 한/영 판단은 상위(파이프라인)가 전역 후킹으로 본 한/영 토글키로 추적한다.
/// 조회 전용이라 포커스/조합에 영향 없음. 결과는 200ms 캐시.
/// </summary>
/// <summary>IME 판정 세부(진단용). 어느 단계에서 막혔는지 특정한다.</summary>
public readonly record struct ImeDiag(uint LangId, bool ImeWndFound, bool Definitive, bool Open, int ConvMode, bool NativeOn);

public sealed class ImeStateReader
{
    private const long CacheMs = 200;

    private bool _cachedDefinitive;
    private bool _cachedHangul;
    private long _cachedAtMs = long.MinValue;
    private ImeDiag _lastDiag;

    /// <summary>마지막 판정의 세부 상태(진단용).</summary>
    public ImeDiag LastDiag => _lastDiag;

    /// <summary>
    /// 포그라운드 앱의 한/영을 확실히 읽을 수 있으면 true 를 반환하고 <paramref name="hangul"/>을 채운다.
    /// 최신 TSF 앱처럼 읽을 수 없으면 false(미확정) — 이때 <paramref name="hangul"/>은 의미 없다.
    /// </summary>
    public bool TryQueryDefinitive(out bool hangul)
    {
        long now = Environment.TickCount64;
        if (now - _cachedAtMs >= CacheMs)
        {
            _cachedDefinitive = Query(out _cachedHangul, out _lastDiag);
            _cachedAtMs = now;
        }
        hangul = _cachedHangul;
        return _cachedDefinitive;
    }

    /// <summary>
    /// 포그라운드 키보드 레이아웃이 '확실히' 비한국어면 true. langId 를 못 읽으면(0) false 로 보고한다
    /// (감지 누락 방지 — fail-open). 조회 전용(GetKeyboardLayout)이라 포커스/조합에 영향이 없다.
    /// TSF 미확정 구간에서 Win+Space 등으로 영문 키보드로 바뀐 경우를 걸러내는 보조 게이트.
    /// </summary>
    public bool ForegroundLayoutIsNonKorean()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        uint threadId = NativeMethods.GetWindowThreadProcessId(fg, out _);
        IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);
        uint langId = (uint)(hkl.ToInt64() & 0xFFFF);
        if (langId == 0) return false;                  // 못 읽음 → 통과(fail-open)

        const uint LANG_KOREAN = 0x12;
        return (langId & 0x3FF) != LANG_KOREAN;         // 주 언어가 한국어가 아니면 비한국어
    }

    /// <summary>확답을 얻으면 true(+hangul). 미확정이면 false.</summary>
    private static bool Query(out bool hangul, out ImeDiag diag)
    {
        hangul = false;
        diag = default;

        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        uint threadId = NativeMethods.GetWindowThreadProcessId(fg, out _);

        // 참고용 LANGID(교차 프로세스에서는 0일 수 있어 판정 근거로 쓰지 않음)
        IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);
        uint langId = (uint)(hkl.ToInt64() & 0xFFFF);

        // 포커스 컨트롤(있으면) 우선 — 그 창의 기본 IME 창을 얻는다.
        IntPtr target = fg;
        var gti = new NativeMethods.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (NativeMethods.GetGUIThreadInfo(threadId, ref gti) && gti.hwndFocus != IntPtr.Zero)
            target = gti.hwndFocus;

        IntPtr imeWnd = NativeMethods.ImmGetDefaultIMEWnd(target);
        if (imeWnd == IntPtr.Zero) imeWnd = NativeMethods.ImmGetDefaultIMEWnd(fg);
        if (imeWnd == IntPtr.Zero)
        {
            diag = new ImeDiag(langId, false, false, false, 0, false);
            return false;   // 미확정(최신 TSF 앱) → 상위의 토글 추적에 맡긴다
        }

        long convRes = QueryIme(imeWnd, NativeMethods.IMC_GETCONVERSIONMODE, out bool okConv);
        if (!okConv)
        {
            diag = new ImeDiag(langId, true, false, false, 0, false);
            return false;   // IME 창은 있으나 응답 없음 → 미확정
        }

        long openRes = QueryIme(imeWnd, NativeMethods.IMC_GETOPENSTATUS, out bool okOpen);
        bool open = !okOpen || openRes != 0;   // 열림 상태를 못 읽으면 열린 것으로 간주
        int conv = (int)convRes;
        bool nativeOn = (conv & NativeMethods.IME_CMODE_NATIVE) != 0;

        hangul = open && nativeOn;
        diag = new ImeDiag(langId, true, true, open, conv, nativeOn);
        return true;   // 확답
    }

    /// <summary>기본 IME 창에 상태를 질의(조회 전용). 응답 없으면 80ms 후 실패 처리.</summary>
    private static long QueryIme(IntPtr imeWnd, int command, out bool ok)
    {
        ok = NativeMethods.SendMessageTimeout(
                 imeWnd, NativeMethods.WM_IME_CONTROL, (IntPtr)command, IntPtr.Zero,
                 NativeMethods.SMTO_ABORTIFHUNG, 80, out IntPtr result) != IntPtr.Zero;
        return result.ToInt64();
    }
}
