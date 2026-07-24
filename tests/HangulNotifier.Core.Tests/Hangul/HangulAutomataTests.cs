using FluentAssertions;
using HangulNotifier.Core.Hangul;

namespace HangulNotifier.Core.Tests.Hangul;

/// <summary>
/// 두벌식 오토마타 테스트. QWERTY 키 시퀀스를 그대로 넣어 완성형 복원을 검증한다.
/// (저수준 후킹은 IME보다 먼저라 raw 키가 들어오므로 오토마타가 직접 조합한다.)
/// </summary>
public class HangulAutomataTests
{
    private static string Type(string qwerty)
    {
        var a = new HangulAutomata();
        foreach (var c in qwerty) a.Feed(c);
        return a.Current;
    }

    // ── 기본 조합 (초성+중성, +종성) ──────────────────────────────
    [Theory]
    [InlineData("rk", "가")]       // ㄱㅏ
    [InlineData("dk", "아")]       // ㅇㅏ
    [InlineData("dks", "안")]      // ㅇㅏㄴ
    [InlineData("gks", "한")]      // ㅎㅏㄴ
    [InlineData("rkd", "강")]      // ㄱㅏㅇ
    public void 기본_음절_조합(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── 복합 중성 (이중모음) ──────────────────────────────────────
    [Theory]
    [InlineData("ehl", "되")]      // ㄷㅗㅣ → ㅚ
    [InlineData("eho", "돼")]      // ㄷㅗㅐ → ㅙ
    [InlineData("dml", "의")]      // ㅇㅡㅣ → ㅢ
    [InlineData("dhk", "와")]      // ㅇㅗㅏ → ㅘ
    [InlineData("gnl", "휘")]      // ㅎㅜㅣ → ㅟ
    public void 복합_중성_조합(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── Shift: 쌍자음 / 이중모음 ───────────────────────────────────
    [Theory]
    [InlineData("Rk", "까")]       // ㄲㅏ
    [InlineData("Tkd", "쌍")]      // ㅆㅏㅇ
    [InlineData("ehoT", "됐")]     // ㄷㅗㅐㅆ
    [InlineData("dO", "얘")]       // ㅇㅒ
    [InlineData("dP", "예")]       // ㅇㅖ
    [InlineData("Tho", "쐐")]      // ㅆㅗㅐ → ㅙ
    public void Shift_조합(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── 복합 종성 (겹받침) ────────────────────────────────────────
    [Theory]
    [InlineData("dksg", "않")]     // ㅇㅏㄴㅎ → ㄶ
    [InlineData("dksw", "앉")]     // ㅇㅏㄴㅈ → ㄵ
    [InlineData("dlfr", "읽")]     // ㅇㅣㄹㄱ → ㄺ
    [InlineData("qkfq", "밟")]     // ㅂㅏㄹㅂ → ㄼ
    [InlineData("rkqt", "값")]     // ㄱㅏㅂㅅ → ㅄ
    public void 복합_종성_조합(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── 종성 이월 (핵심) ──────────────────────────────────────────
    // 종성이 있는 상태에서 모음이 오면 종성을 다음 글자 초성으로 넘긴다.
    [Theory]
    [InlineData("dksgdk", "않아")]   // 않 + (ㅇ)ㅏ → 받침 유지, 새 음절
    [InlineData("dksgk", "안하")]    // 않 + ㅏ → 겹받침 뒤쪽(ㅎ)만 이월
    [InlineData("dlfrdj", "읽어")]   // 읽 + (ㅇ)ㅓ → 유지
    [InlineData("dlfrj", "일거")]    // 읽 + ㅓ → 겹받침 뒤쪽(ㄱ)만 이월
    [InlineData("rkaj", "가머")]     // 감 + ㅓ → 단일 종성 ㅁ 이월 (ㄱㅏㅁㅓ)
    public void 종성_이월(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── 여러 음절 어절 ────────────────────────────────────────────
    [Theory]
    [InlineData("dkssudgktpdy", "안녕하세요")]
    [InlineData("gksrmf", "한글")]         // ㅎㅏㄴㄱㅡㄹ
    [InlineData("ehody", "돼요")]          // ㄷㅗㅐㅇㅛ
    public void 어절_조합(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── Backspace: 조합 중 자모 단위 되돌리기 ─────────────────────
    [Fact]
    public void Backspace_복합중성_단계적_되돌리기()
    {
        var a = new HangulAutomata();
        foreach (var c in "ehl") a.Feed(c);   // 되
        a.Current.Should().Be("되");
        a.Backspace();                          // ㅚ → ㅗ
        a.Current.Should().Be("도");
        a.Backspace();                          // ㅗ 제거 → ㄷ
        a.Current.Should().Be("ㄷ");
        a.Backspace();                          // ㄷ 제거 → 없음
        a.Current.Should().Be("");
    }

    [Fact]
    public void Backspace_겹받침_되돌리기()
    {
        var a = new HangulAutomata();
        foreach (var c in "dlfr") a.Feed(c);   // 읽
        a.Current.Should().Be("읽");
        a.Backspace();                          // ㄺ → ㄹ
        a.Current.Should().Be("일");
        a.Backspace();                          // ㄹ 제거
        a.Current.Should().Be("이");
    }

    [Fact]
    public void Backspace_확정문자_제거()
    {
        var a = new HangulAutomata();
        foreach (var c in "rk") a.Feed(c);      // 가
        a.Feed(' ');                             // 공백 → 확정 + 리터럴
        a.Current.Should().Be("가 ");
        a.Backspace();                           // 공백 제거
        a.Current.Should().Be("가");
        a.Backspace();                           // 가 → ㄱ (조합 없음이므로 확정문자 되돌림 아님: 가 통째 제거)
        a.Current.Should().Be("");
    }

    // ── Reset ─────────────────────────────────────────────────────
    [Fact]
    public void Reset_전체_초기화()
    {
        var a = new HangulAutomata();
        foreach (var c in "dksg") a.Feed(c);    // 않
        a.Reset();
        a.Current.Should().Be("");
        a.Composing.Should().BeNull();
        a.Committed.Should().Be("");
    }

    // ── 비자모 리터럴 통과 ────────────────────────────────────────
    [Theory]
    [InlineData("rk123", "가123")]
    [InlineData("dks.", "안.")]
    public void 비자모_리터럴_통과(string keys, string expected)
        => Type(keys).Should().Be(expected);

    // ── Composing/Committed 분리 ──────────────────────────────────
    [Fact]
    public void 조합중_음절은_Composing_확정은_Committed()
    {
        var a = new HangulAutomata();
        foreach (var c in "dksgdk") a.Feed(c);  // 않아
        a.Committed.Should().Be("않");
        a.Composing.Should().Be('아');
        a.Current.Should().Be("않아");
    }
}
