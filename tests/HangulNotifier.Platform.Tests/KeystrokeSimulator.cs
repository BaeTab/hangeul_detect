using HangulNotifier.Core.Hangul;

namespace HangulNotifier.Platform.Tests;

/// <summary>한 번의 물리 키 입력(가상 키코드 + Shift 상태).</summary>
public readonly record struct Keystroke(int Vk, bool Shift);

/// <summary>
/// 한글 문자열을 "두벌식 키보드에서 그 글자를 치려면 눌러야 하는 실제 키 순서"로 되돌린다.
///
/// <para>기존 규칙 테스트는 완성된 문자열을 엔진에 직접 넘기지만, 실사용에서는 사용자가
/// <b>키를 누르고</b> 그것이 후킹 → KeyTranslator → 오토마타를 거쳐 글자가 된다.
/// 이 시뮬레이터는 그 앞단(키 순서)을 만들어, 규칙이 "실제로 타이핑했을 때"도
/// 감지되는지를 검증할 수 있게 한다.</para>
///
/// <para>앱 코드가 아니라 테스트 전용 역매핑이다. 앱의 정매핑(<see cref="HangulJamo"/>)과
/// 독립적으로 작성해, 한쪽이 틀리면 왕복 검증에서 드러나도록 한다.</para>
/// </summary>
public static class KeystrokeSimulator
{
    private const int VK_SPACE = 0x20;

    // 자모 → 두벌식 키 (대문자 = Shift 필요)
    private static readonly Dictionary<char, char> JamoToKey = new()
    {
        ['ㅂ'] = 'q', ['ㅈ'] = 'w', ['ㄷ'] = 'e', ['ㄱ'] = 'r', ['ㅅ'] = 't',
        ['ㅛ'] = 'y', ['ㅕ'] = 'u', ['ㅑ'] = 'i', ['ㅐ'] = 'o', ['ㅔ'] = 'p',
        ['ㅁ'] = 'a', ['ㄴ'] = 's', ['ㅇ'] = 'd', ['ㄹ'] = 'f', ['ㅎ'] = 'g',
        ['ㅗ'] = 'h', ['ㅓ'] = 'j', ['ㅏ'] = 'k', ['ㅣ'] = 'l',
        ['ㅋ'] = 'z', ['ㅌ'] = 'x', ['ㅊ'] = 'c', ['ㅍ'] = 'v',
        ['ㅠ'] = 'b', ['ㅜ'] = 'n', ['ㅡ'] = 'm',
        // Shift 계열 (쌍자음 · 이중모음)
        ['ㅃ'] = 'Q', ['ㅉ'] = 'W', ['ㄸ'] = 'E', ['ㄲ'] = 'R', ['ㅆ'] = 'T',
        ['ㅒ'] = 'O', ['ㅖ'] = 'P',
    };

    // 복합 모음 → 눌러야 하는 기본 모음 두 개 (ㅘ = ㅗ 다음 ㅏ)
    private static readonly Dictionary<char, string> MedialParts = new()
    {
        ['ㅘ'] = "ㅗㅏ", ['ㅙ'] = "ㅗㅐ", ['ㅚ'] = "ㅗㅣ",
        ['ㅝ'] = "ㅜㅓ", ['ㅞ'] = "ㅜㅔ", ['ㅟ'] = "ㅜㅣ",
        ['ㅢ'] = "ㅡㅣ",
    };

    // 복합 받침 → 눌러야 하는 자음 두 개 (ㄳ = ㄱ 다음 ㅅ)
    private static readonly Dictionary<char, string> FinalParts = new()
    {
        ['ㄳ'] = "ㄱㅅ", ['ㄵ'] = "ㄴㅈ", ['ㄶ'] = "ㄴㅎ",
        ['ㄺ'] = "ㄹㄱ", ['ㄻ'] = "ㄹㅁ", ['ㄼ'] = "ㄹㅂ", ['ㄽ'] = "ㄹㅅ",
        ['ㄾ'] = "ㄹㅌ", ['ㄿ'] = "ㄹㅍ", ['ㅀ'] = "ㄹㅎ",
        ['ㅄ'] = "ㅂㅅ",
    };

    /// <summary>
    /// 한글 문자열을 키 입력 순서로 변환한다. 공백은 스페이스바로 넣는다.
    /// 한글·공백이 아닌 문자가 있으면 <paramref name="keys"/>는 비고 false를 돌려준다
    /// (시뮬레이터가 흉내낼 수 없는 입력을 조용히 건너뛰지 않기 위함).
    /// </summary>
    public static bool TryTypeOut(string text, out List<Keystroke> keys)
    {
        keys = new List<Keystroke>();

        foreach (char ch in text)
        {
            if (ch == ' ')
            {
                keys.Add(new Keystroke(VK_SPACE, false));
                continue;
            }

            if (!TryDecompose(ch, out string jamos)) { keys.Clear(); return false; }

            foreach (char jamo in jamos)
            {
                if (!JamoToKey.TryGetValue(jamo, out char key)) { keys.Clear(); return false; }
                bool shift = char.IsUpper(key);
                int vk = 0x41 + (char.ToLowerInvariant(key) - 'a');   // VK_A..VK_Z
                keys.Add(new Keystroke(vk, shift));
            }
        }

        return true;
    }

    /// <summary>완성형 음절(가–힣)을 눌러야 할 자모 순서로 분해한다.</summary>
    private static bool TryDecompose(char syllable, out string jamos)
    {
        jamos = "";
        if (syllable < '가' || syllable > '힣') return false;

        int code = syllable - HangulJamo.SBase;
        int cho = code / (HangulJamo.JungCount * HangulJamo.JongCount);
        int jung = code % (HangulJamo.JungCount * HangulJamo.JongCount) / HangulJamo.JongCount;
        int jong = code % HangulJamo.JongCount;

        var sb = new System.Text.StringBuilder(6);
        sb.Append(HangulJamo.Cho[cho]);

        char medial = HangulJamo.Jung[jung];
        sb.Append(MedialParts.TryGetValue(medial, out var mp) ? mp : medial.ToString());

        if (jong != 0)
        {
            char final = HangulJamo.Jong[jong];
            sb.Append(FinalParts.TryGetValue(final, out var fp) ? fp : final.ToString());
        }

        jamos = sb.ToString();
        return true;
    }
}
