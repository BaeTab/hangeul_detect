namespace HangulNotifier.Platform.Input;

/// <summary>
/// 저수준 후킹이 캡처한 단일 키 이벤트. 후킹 콜백에서 Channel로 넘겨 워커가 소비한다.
/// </summary>
public readonly record struct KeyEvent(
    int VirtualKeyCode,
    bool IsKeyDown,
    bool ShiftDown,
    long TimestampMs);
