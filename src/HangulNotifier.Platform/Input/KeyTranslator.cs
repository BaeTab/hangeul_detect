namespace HangulNotifier.Platform.Input;

public enum KeyAction
{
    None,        // 무시 (수식키 등)
    Character,   // 문자 입력 (오토마타로 Feed)
    Backspace,   // 되돌리기
    Boundary,    // 어절 확정 (공백/Enter/Tab/문장부호)
    Reset,       // 강제 리셋 (방향키/Home/End/Delete/Esc)
}

public readonly record struct TranslatedKey(KeyAction Action, char Character);

/// <summary>
/// 가상 키코드를 어절 버퍼용 의미 동작으로 변환한다. 순수 로직(P/Invoke 없음).
/// 알파벳은 두벌식 매핑을 위해 raw QWERTY 문자(a–z / A–Z)를 넘긴다.
/// </summary>
public static class KeyTranslator
{
    // 가상 키코드
    private const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_ESCAPE = 0x1B, VK_SPACE = 0x20;
    private const int VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24;
    private const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
    private const int VK_INSERT = 0x2D, VK_DELETE = 0x2E;
    private const int VK_0 = 0x30, VK_9 = 0x39;
    private const int VK_A = 0x41, VK_Z = 0x5A;
    private const int VK_NUMPAD0 = 0x60, VK_NUMPAD9 = 0x69;

    public static TranslatedKey Translate(int vk, bool shift)
    {
        // 알파벳 → raw QWERTY 문자 (Shift로 대소문자 결정; 두벌식 쌍자음/이중모음 매핑에 사용)
        if (vk is >= VK_A and <= VK_Z)
        {
            char baseCh = (char)('a' + (vk - VK_A));
            char ch = shift ? char.ToUpperInvariant(baseCh) : baseCh;
            return new TranslatedKey(KeyAction.Character, ch);
        }

        // 숫자 (윗줄) — Shift면 기호이므로 경계로 취급
        if (vk is >= VK_0 and <= VK_9)
            return shift
                ? new TranslatedKey(KeyAction.Boundary, '\0')
                : new TranslatedKey(KeyAction.Character, (char)('0' + (vk - VK_0)));

        // 숫자패드
        if (vk is >= VK_NUMPAD0 and <= VK_NUMPAD9)
            return new TranslatedKey(KeyAction.Character, (char)('0' + (vk - VK_NUMPAD0)));

        return vk switch
        {
            VK_BACK => new TranslatedKey(KeyAction.Backspace, '\0'),
            VK_SPACE => new TranslatedKey(KeyAction.Boundary, ' '),
            VK_RETURN => new TranslatedKey(KeyAction.Boundary, '\n'),
            VK_TAB => new TranslatedKey(KeyAction.Boundary, '\t'),

            VK_LEFT or VK_UP or VK_RIGHT or VK_DOWN
                or VK_HOME or VK_END or VK_PRIOR or VK_NEXT
                or VK_DELETE or VK_INSERT or VK_ESCAPE
                => new TranslatedKey(KeyAction.Reset, '\0'),

            // 그 외(문장부호·기호 OEM 키 등)는 어절 경계로 취급
            _ when IsPunctuationOem(vk) => new TranslatedKey(KeyAction.Boundary, '\0'),

            _ => new TranslatedKey(KeyAction.None, '\0'),
        };
    }

    // OEM 문장부호/기호 키 범위 (레이아웃 의존이므로 경계로만 사용)
    private static bool IsPunctuationOem(int vk)
        => vk is 0xBA or 0xBB or 0xBC or 0xBD or 0xBE or 0xBF or 0xC0
             or 0xDB or 0xDC or 0xDD or 0xDE;
}
