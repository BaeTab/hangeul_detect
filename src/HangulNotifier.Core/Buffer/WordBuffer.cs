using HangulNotifier.Core.Hangul;

namespace HangulNotifier.Core.Buffer;

/// <summary>판정 요청: 현재 어절과 직전 어절(띄어쓰기 케이스 판정용).</summary>
public readonly record struct WordCheck(string Word, string? PreviousWord);

/// <summary>
/// 어절 단위로 텍스트를 모으고 판정 트리거를 관리한다. 순수 로직(시간은 nowMs로 주입).
/// - 확정 트리거: 공백/Enter/Tab/문장부호
/// - 디바운스: 마지막 입력 후 400ms 정지
/// - 강제 리셋: 창 변경/클릭/방향키/30초 무입력 (호출자가 ForceReset/Tick으로 유도)
/// </summary>
public sealed class WordBuffer
{
    public const int MaxWordLength = 64;
    public const long DebounceMs = 400;
    public const long IdleResetMs = 30_000;

    // 문장부호 경계 (공백/탭은 별도 처리)
    private static readonly HashSet<char> BoundaryPunctuation = new()
    {
        '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']',
    };

    private readonly HangulAutomata _automata = new();
    private string? _previousWord;
    private long _lastInputMs;
    private bool _debouncePending;

    public event Action<WordCheck>? CheckRequested;

    public string CurrentWord => _automata.Current;

    /// <summary>인쇄 가능한 키 하나. 경계 문자면 어절을 확정한다.</summary>
    public void FeedChar(char c, long nowMs)
    {
        _lastInputMs = nowMs;
        if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || BoundaryPunctuation.Contains(c))
        {
            FinalizeWord();
            return;
        }

        _automata.Feed(c);
        if (_automata.Current.Length > MaxWordLength)
        {
            // 어절이 너무 길다 — 현재 어절만 버린다(이전어절은 유지).
            _automata.Reset();
            _debouncePending = false;
            return;
        }
        _debouncePending = true;
    }

    public void Backspace(long nowMs)
    {
        _lastInputMs = nowMs;
        _automata.Backspace();
        _debouncePending = true;
    }

    /// <summary>Enter/Tab 등 제어키 경계.</summary>
    public void CommitBoundary(long nowMs)
    {
        _lastInputMs = nowMs;
        FinalizeWord();
    }

    /// <summary>창 변경/마우스 클릭/방향키 등 — 현재·직전 어절 모두 버린다.</summary>
    public void ForceReset()
    {
        _automata.Reset();
        _previousWord = null;
        _debouncePending = false;
    }

    /// <summary>주기적 호출. 디바운스 판정과 30초 무입력 리셋을 처리한다.</summary>
    public void Tick(long nowMs)
    {
        if (_lastInputMs > 0 && nowMs - _lastInputMs >= IdleResetMs)
        {
            ForceReset();
            return;
        }
        if (_debouncePending && nowMs - _lastInputMs >= DebounceMs)
        {
            _debouncePending = false;
            var w = _automata.Current;
            if (w.Length > 0) CheckRequested?.Invoke(new WordCheck(w, _previousWord));
        }
    }

    private void FinalizeWord()
    {
        var w = _automata.Current;
        _automata.Reset();
        _debouncePending = false;
        if (w.Length == 0) return;

        CheckRequested?.Invoke(new WordCheck(w, _previousWord));
        _previousWord = w;
    }
}
