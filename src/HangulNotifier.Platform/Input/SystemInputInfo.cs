using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Input;

/// <summary>
/// 시스템 전역의 마지막 입력 '시각'만 조회한다(GetLastInputInfo).
/// 입력 내용·키 코드는 전혀 얻지 않으며, 후킹이 살아있는지 확인하는 용도로만 쓴다.
/// </summary>
public static class SystemInputInfo
{
    /// <summary>마지막 입력 이후 경과 ms. 조회 실패 시 0(=방금 입력)으로 본다.</summary>
    public static long IdleMs()
    {
        var lii = new NativeMethods.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref lii)) return 0;

        // dwTime 은 GetTickCount(32bit) 기준이라 약 49.7일마다 순환한다.
        // 부호 없는 뺄셈으로 계산하면 순환 구간에서도 올바른 경과 시간이 나온다.
        uint now = unchecked((uint)Environment.TickCount);
        return unchecked(now - lii.dwTime);
    }

    /// <summary>마지막 입력 시각을 TickCount64 축으로 환산. 후킹 콜백 시각과 직접 비교하기 위함.</summary>
    public static long LastInputTicks64() => Environment.TickCount64 - IdleMs();
}
