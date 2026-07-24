using FluentAssertions;
using HangulNotifier.Core.Buffer;

namespace HangulNotifier.Core.Tests.Buffer;

public class WordBufferTests
{
    private static (WordBuffer buf, List<WordCheck> checks) Make()
    {
        var buf = new WordBuffer();
        var checks = new List<WordCheck>();
        buf.CheckRequested += c => checks.Add(c);
        return (buf, checks);
    }

    private static void TypeKeys(WordBuffer buf, string keys, long startMs = 1000)
    {
        long t = startMs;
        foreach (var c in keys) buf.FeedChar(c, t++);
    }

    [Fact]
    public void 공백_경계에서_어절_확정하고_이전어절로_설정()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "dks", 1000);       // 안
        buf.FeedChar(' ', 1010);          // 경계 → 확정
        checks.Should().ContainSingle();
        checks[0].Word.Should().Be("안");
        checks[0].PreviousWord.Should().BeNull();

        TypeKeys(buf, "ehl", 1020);       // 되
        buf.FeedChar(' ', 1030);          // 경계 → 확정, 이전어절 "안"
        checks.Should().HaveCount(2);
        checks[1].Word.Should().Be("되");
        checks[1].PreviousWord.Should().Be("안");
    }

    [Fact]
    public void 문장부호_경계에서_확정()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "ehl", 1000);       // 되
        buf.FeedChar('.', 1010);
        checks.Should().ContainSingle();
        checks[0].Word.Should().Be("되");
    }

    [Fact]
    public void 디바운스_400ms_정지시_판정()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "ehl", 1000);       // 되, 마지막 입력 1002
        buf.Tick(1400);                    // 398ms 경과 → 아직
        checks.Should().BeEmpty();
        buf.Tick(1402);                    // 400ms 경과 → 판정
        checks.Should().ContainSingle();
        checks[0].Word.Should().Be("되");
    }

    [Fact]
    public void 디바운스_판정후_추가입력없으면_재판정_안함()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "ehl", 1000);
        buf.Tick(1500);
        buf.Tick(2000);
        checks.Should().ContainSingle();   // 한 번만
    }

    [Fact]
    public void 강제리셋시_현재어절_비움_이전어절_비움()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "dks", 1000);        // 안
        buf.ForceReset();
        buf.CurrentWord.Should().Be("");
        buf.Tick(2000);
        checks.Should().BeEmpty();
    }

    [Fact]
    public void 무입력_30초_경과시_강제리셋()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "dks", 1000);
        buf.Tick(31_005);                  // 30초+ 경과
        buf.CurrentWord.Should().Be("");
    }

    [Fact]
    public void Backspace로_조합_되돌림()
    {
        var (buf, _) = Make();
        TypeKeys(buf, "ehl", 1000);        // 되
        buf.Backspace(1010);
        buf.CurrentWord.Should().Be("도");
    }

    [Fact]
    public void Enter_Tab_경계도_확정()
    {
        var (buf, checks) = Make();
        TypeKeys(buf, "ehl", 1000);
        buf.CommitBoundary(1010);          // Enter/Tab
        checks.Should().ContainSingle();
        checks[0].Word.Should().Be("되");
    }

    [Fact]
    public void 어절_64자_초과시_리셋()
    {
        var (buf, _) = Make();
        long t = 1000;
        // 'k'(ㅏ)는 종성 없이 계속 단독/이월되며 길이가 늘어나는 대신
        // 자음+모음 반복으로 긴 어절 생성: "rk" 반복 = 가가가...
        for (int i = 0; i < 70; i++) { buf.FeedChar('r', t++); buf.FeedChar('k', t++); }
        buf.CurrentWord.Length.Should().BeLessThanOrEqualTo(WordBuffer.MaxWordLength);
    }

    [Fact]
    public void 빈_어절에서_공백은_판정_안함()
    {
        var (buf, checks) = Make();
        buf.FeedChar(' ', 1000);
        checks.Should().BeEmpty();
    }
}
