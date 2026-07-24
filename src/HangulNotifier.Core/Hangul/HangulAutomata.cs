using System.Text;

namespace HangulNotifier.Core.Hangul;

/// <summary>
/// 두벌식 조합 오토마타. QWERTY 키 char 스트림을 받아 완성형 음절로 복원한다.
/// 저수준 후킹은 IME보다 먼저 실행되므로 raw 키가 들어온다 — 직접 조합한다.
/// 텍스트를 수정하거나 주입하지 않는다. 오직 복원만 한다.
/// </summary>
public sealed class HangulAutomata
{
    private readonly StringBuilder _committed = new();

    // 조합 중 음절 상태 (인덱스). -1/-1/0 = 비어있음.
    private int _cho = -1;
    private int _jung = -1;
    private int _jong = 0;

    /// <summary>확정된 문자열.</summary>
    public string Committed => _committed.ToString();

    /// <summary>조합 중인 음절(또는 단독 자모). 없으면 null.</summary>
    public char? Composing
    {
        get
        {
            if (_cho >= 0 && _jung >= 0) return HangulJamo.Compose(_cho, _jung, _jong);
            if (_cho >= 0) return HangulJamo.Cho[_cho];
            if (_jung >= 0) return HangulJamo.Jung[_jung];
            return null;
        }
    }

    public string Current => Committed + (Composing?.ToString() ?? "");

    /// <summary>키 하나를 먹인다. 두벌식 자모면 조합, 아니면 리터럴 통과.</summary>
    public void Feed(char jamoOrChar)
    {
        if (!HangulJamo.TryMapKey(jamoOrChar, out var jamo))
        {
            // 비자모(공백/문장부호/숫자/영문 등): 조합 확정 후 리터럴 추가
            CommitComposing();
            _committed.Append(jamoOrChar);
            return;
        }

        if (HangulJamo.IsVowel(jamo)) FeedVowel(jamo);
        else FeedConsonant(jamo);
    }

    private void FeedVowel(char jamo)
    {
        int vidx = HangulJamo.GetJungIndex(jamo);

        // 1) 종성 이월: 종성이 있는 상태에서 모음 → 종성(복합이면 뒤쪽)을 다음 글자 초성으로.
        if (_jong != 0)
        {
            int movedCho;
            if (HangulJamo.TrySplitFinal(_jong, out int first, out char moved))
            {
                _jong = first;                                  // 앞 자모만 현재 음절에 남김
                movedCho = HangulJamo.GetChoIndex(moved);
            }
            else
            {
                movedCho = HangulJamo.GetChoIndex(HangulJamo.Jong[_jong]); // 단일 종성 전체 이월
                _jong = 0;
            }
            _committed.Append(HangulJamo.Compose(_cho, _jung, _jong)); // 현재 음절 확정
            _cho = movedCho; _jung = vidx; _jong = 0;                  // 새 음절 시작
            return;
        }

        // 2) 초성만 있고 중성 없음 → 중성 채움
        if (_cho >= 0 && _jung < 0) { _jung = vidx; return; }

        // 3) 중성이 이미 있음 → 복합 중성 결합 시도, 실패하면 확정 후 새 (단독)모음
        if (_jung >= 0)
        {
            if (HangulJamo.TryCombineMedial(_jung, jamo, out int combined)) { _jung = combined; return; }
            CommitComposing();
            _jung = vidx;
            return;
        }

        // 4) 완전 비어있음 → 단독 모음
        _jung = vidx;
    }

    private void FeedConsonant(char jamo)
    {
        // 1) 초성+중성 있고 종성 없음 → 종성 시도
        if (_cho >= 0 && _jung >= 0 && _jong == 0)
        {
            int jidx = HangulJamo.GetJongIndex(jamo);
            if (jidx > 0) { _jong = jidx; return; }
            // 종성 불가 자음(ㄸㅃㅉ) → 확정 후 새 초성
            CommitComposing();
            _cho = HangulJamo.GetChoIndex(jamo);
            return;
        }

        // 2) 종성 있음 → 복합 종성 결합 시도, 실패하면 확정 후 새 초성
        if (_jong != 0)
        {
            if (HangulJamo.TryCombineFinal(_jong, jamo, out int combined)) { _jong = combined; return; }
            CommitComposing();
            _cho = HangulJamo.GetChoIndex(jamo);
            return;
        }

        // 3) 단독 자음, 또는 4) 단독 모음 뒤 → 확정 후 새 초성
        if (_cho >= 0 || _jung >= 0)
        {
            CommitComposing();
            _cho = HangulJamo.GetChoIndex(jamo);
            return;
        }

        // 5) 완전 비어있음 → 단독 초성
        _cho = HangulJamo.GetChoIndex(jamo);
    }

    /// <summary>조합 중이면 마지막 자모 단위로 되돌린다. 조합이 없으면 확정 마지막 글자를 제거.</summary>
    public void Backspace()
    {
        if (_jong != 0)
        {
            _jong = HangulJamo.TrySplitFinal(_jong, out int first, out _) ? first : 0;
            return;
        }
        if (_jung >= 0)
        {
            _jung = HangulJamo.TrySplitMedial(_jung, out int baseIdx) ? baseIdx : -1;
            return;
        }
        if (_cho >= 0) { _cho = -1; return; }

        if (_committed.Length > 0) _committed.Remove(_committed.Length - 1, 1);
    }

    public void Reset()
    {
        _committed.Clear();
        _cho = -1; _jung = -1; _jong = 0;
    }

    private void CommitComposing()
    {
        var c = Composing;
        if (c.HasValue) _committed.Append(c.Value);
        _cho = -1; _jung = -1; _jong = 0;
    }
}
