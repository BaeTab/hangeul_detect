using System.Runtime.InteropServices;
using System.Threading.Channels;
using HangulNotifier.Platform.Input;
using HangulNotifier.Platform.Native;

namespace HangulNotifier.Platform.Hooking;

/// <summary>
/// WH_KEYBOARD_LL 저수준 전역 키보드 후킹.
///
/// 원칙:
/// - 콜백은 즉시 반환한다(무거운 작업 금지). KeyEvent를 Channel에 넣기만 하고, 소비는 워커 스레드에서.
/// - 항상 CallNextHookEx로 통과시킨다. 절대 입력을 소비하지 않는다.
/// - 델리게이트를 인스턴스 필드로 유지해 GC 수거를 막는다(앱이 인스턴스를 루트로 보유).
/// - 후킹은 자체 메시지 루프를 가진 전용 스레드에서 설치/해제한다(UI 스레드 부담 방지).
/// - 일시정지 = 후킹 해제(Stop). 플래그로만 무시하지 않는다.
///
/// 백신 하드닝: 비주입형 후킹(DLL 주입 아님). 입력을 어디에도 저장/전송하지 않는다.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly Channel<KeyEvent> _channel =
        Channel.CreateUnbounded<KeyEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    // GC 수거 방지를 위해 델리게이트를 필드로 고정.
    private readonly NativeMethods.LowLevelKeyboardProc _proc;

    private IntPtr _hookHandle = IntPtr.Zero;
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _running;

    public event Action? Installed;
    public event Action? Uninstalled;

    /// <summary>소비자(워커)가 읽는 이벤트 스트림.</summary>
    public ChannelReader<KeyEvent> Reader => _channel.Reader;

    public bool IsRunning => _running;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    /// <summary>후킹 시작(재개). 이미 실행 중이면 무시.</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "HangulKeyboardHook",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>후킹 해제(일시정지). 메시지 루프에 WM_QUIT을 보내 스레드를 정리한다.</summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        if (_threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _threadId = 0;
    }

    private void HookThreadProc()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);

        if (_hookHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            _running = false;
            throw new InvalidOperationException($"SetWindowsHookEx 실패 (Win32 오류 {err})");
        }

        Installed?.Invoke();

        // 메시지 루프 — LL 후킹은 설치 스레드가 메시지를 펌프해야 콜백이 온다.
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        Uninstalled?.Invoke();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 반드시 즉시 반환. 여기서 정규식/DB/파일IO/UI 조작 금지.
        if (nCode == NativeMethods.HC_ACTION)
        {
            int msg = (int)wParam;
            bool down = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool up = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;
            if (down || up)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                _channel.Writer.TryWrite(new KeyEvent((int)data.vkCode, down, shift, Environment.TickCount64));
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        _channel.Writer.TryComplete();
    }
}
