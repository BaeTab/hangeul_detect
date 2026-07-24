namespace HangulNotifier.Core.Hangul;

/// <summary>
/// 두벌식 자모 상수 테이블과 인덱스/결합 헬퍼. 순수 데이터·함수 (UI/Win32 의존 없음).
/// 유니코드 음절 = 0xAC00 + (초성 * 21 + 중성) * 28 + 종성.
/// </summary>
public static class HangulJamo
{
    public const char SBase = '가';      // '가'
    public const int JungCount = 21;
    public const int JongCount = 28;

    /// <summary>초성 19 (순서 = 인덱스)</summary>
    public const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    /// <summary>중성 21 (순서 = 인덱스)</summary>
    public const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    /// <summary>종성 28 (인덱스 0 = 없음)</summary>
    public const string Jong = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

    private static readonly Dictionary<char, char> Keymap = BuildKeymap();
    private static readonly Dictionary<char, int> ChoIdx = BuildIndex(Cho, from: 0);
    private static readonly Dictionary<char, int> JungIdx = BuildIndex(Jung, from: 0);
    private static readonly Dictionary<char, int> JongIdx = BuildIndex(Jong, from: 1); // 0 = 없음 제외

    // 복합 중성 결합: (현재 중성 인덱스, 추가 모음 char) -> 결합 인덱스
    private static readonly Dictionary<(int, char), int> MedialCombine = new()
    {
        {(8,  'ㅏ'), 9}, {(8,  'ㅐ'), 10}, {(8,  'ㅣ'), 11},   // ㅗ + ㅏ/ㅐ/ㅣ = ㅘ/ㅙ/ㅚ
        {(13, 'ㅓ'), 14}, {(13, 'ㅔ'), 15}, {(13, 'ㅣ'), 16},  // ㅜ + ㅓ/ㅔ/ㅣ = ㅝ/ㅞ/ㅟ
        {(18, 'ㅣ'), 19},                                       // ㅡ + ㅣ = ㅢ
    };
    // 복합 중성 분해 (Backspace): 결합 인덱스 -> 기본 인덱스
    private static readonly Dictionary<int, int> MedialSplit = new()
    {
        {9, 8}, {10, 8}, {11, 8}, {14, 13}, {15, 13}, {16, 13}, {19, 18},
    };

    // 복합 종성 결합: (현재 종성 인덱스, 추가 자음 char) -> 결합 인덱스
    private static readonly Dictionary<(int, char), int> FinalCombine = new()
    {
        {(1, 'ㅅ'), 3},                                   // ㄱㅅ = ㄳ
        {(4, 'ㅈ'), 5}, {(4, 'ㅎ'), 6},                   // ㄴㅈ = ㄵ, ㄴㅎ = ㄶ
        {(8, 'ㄱ'), 9}, {(8, 'ㅁ'), 10}, {(8, 'ㅂ'), 11},
        {(8, 'ㅅ'), 12}, {(8, 'ㅌ'), 13}, {(8, 'ㅍ'), 14}, {(8, 'ㅎ'), 15}, // ㄹ + …
        {(17, 'ㅅ'), 18},                                 // ㅂㅅ = ㅄ
    };
    // 복합 종성 분해 (Backspace / 이월): 결합 인덱스 -> (앞 종성 인덱스, 뒤 자음 char)
    private static readonly Dictionary<int, (int first, char moved)> FinalSplit = new()
    {
        {3, (1, 'ㅅ')}, {5, (4, 'ㅈ')}, {6, (4, 'ㅎ')},
        {9, (8, 'ㄱ')}, {10, (8, 'ㅁ')}, {11, (8, 'ㅂ')}, {12, (8, 'ㅅ')},
        {13, (8, 'ㅌ')}, {14, (8, 'ㅍ')}, {15, (8, 'ㅎ')},
        {18, (17, 'ㅅ')},
    };

    public static bool TryMapKey(char qwerty, out char jamo) => Keymap.TryGetValue(qwerty, out jamo);
    public static bool IsVowel(char jamo) => JungIdx.ContainsKey(jamo);
    public static bool IsConsonant(char jamo) => ChoIdx.ContainsKey(jamo);

    public static int GetChoIndex(char c) => ChoIdx.TryGetValue(c, out var i) ? i : -1;
    public static int GetJungIndex(char c) => JungIdx.TryGetValue(c, out var i) ? i : -1;
    /// <summary>종성 인덱스. 종성이 될 수 없는 자음(ㄸㅃㅉ)은 -1.</summary>
    public static int GetJongIndex(char c) => JongIdx.TryGetValue(c, out var i) ? i : -1;

    public static bool TryCombineMedial(int cur, char add, out int combined)
        => MedialCombine.TryGetValue((cur, add), out combined);
    public static bool TrySplitMedial(int combined, out int baseIdx)
        => MedialSplit.TryGetValue(combined, out baseIdx);

    public static bool TryCombineFinal(int cur, char add, out int combined)
        => FinalCombine.TryGetValue((cur, add), out combined);
    public static bool TrySplitFinal(int combined, out int first, out char moved)
    {
        if (FinalSplit.TryGetValue(combined, out var t)) { first = t.first; moved = t.moved; return true; }
        first = 0; moved = '\0'; return false;
    }

    public static char Compose(int cho, int jung, int jong)
        => (char)(SBase + (cho * JungCount + jung) * JongCount + jong);

    private static Dictionary<char, int> BuildIndex(string table, int from)
    {
        var d = new Dictionary<char, int>();
        for (int i = from; i < table.Length; i++)
            if (table[i] != '\0') d[table[i]] = i;
        return d;
    }

    private static Dictionary<char, char> BuildKeymap()
    {
        // 두벌식 표준 (소문자 기준)
        var lower = new Dictionary<char, char>
        {
            ['q'] = 'ㅂ', ['w'] = 'ㅈ', ['e'] = 'ㄷ', ['r'] = 'ㄱ', ['t'] = 'ㅅ',
            ['y'] = 'ㅛ', ['u'] = 'ㅕ', ['i'] = 'ㅑ', ['o'] = 'ㅐ', ['p'] = 'ㅔ',
            ['a'] = 'ㅁ', ['s'] = 'ㄴ', ['d'] = 'ㅇ', ['f'] = 'ㄹ', ['g'] = 'ㅎ',
            ['h'] = 'ㅗ', ['j'] = 'ㅓ', ['k'] = 'ㅏ', ['l'] = 'ㅣ',
            ['z'] = 'ㅋ', ['x'] = 'ㅌ', ['c'] = 'ㅊ', ['v'] = 'ㅍ',
            ['b'] = 'ㅠ', ['n'] = 'ㅜ', ['m'] = 'ㅡ',
        };
        var map = new Dictionary<char, char>(lower);
        // 대문자 기본값은 소문자와 동일
        foreach (var kv in lower)
            map[char.ToUpperInvariant(kv.Key)] = kv.Value;
        // Shift 특수: 쌍자음 / 이중모음
        map['Q'] = 'ㅃ'; map['W'] = 'ㅉ'; map['E'] = 'ㄸ'; map['R'] = 'ㄲ'; map['T'] = 'ㅆ';
        map['O'] = 'ㅒ'; map['P'] = 'ㅖ';
        return map;
    }
}
