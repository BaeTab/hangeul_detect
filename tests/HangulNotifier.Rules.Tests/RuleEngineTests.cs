using FluentAssertions;
using HangulNotifier.Core.Rules;

namespace HangulNotifier.Rules.Tests;

public class RuleEngineTests
{
    private static RuleEngine Engine(Action<RuleEngineOptions>? cfg = null)
    {
        var opts = new RuleEngineOptions();
        cfg?.Invoke(opts);
        return new RuleEngine(RuleSet.LoadDefault().Rules, opts);
    }

    // ── Certain: 무조건 오류 감지 ────────────────────────────────
    [Theory]
    [InlineData("됬다", "됐")]
    [InlineData("되요", "돼요")]
    [InlineData("되서", "돼서")]
    [InlineData("되야", "돼야")]
    [InlineData("돼고", "되")]
    [InlineData("않되", "안 돼")]
    [InlineData("몇일", "며칠")]
    [InlineData("오랫만", "오랜만")]
    [InlineData("설레임", "설렘")]
    [InlineData("희안", "희한")]
    [InlineData("왠만", "웬만")]
    [InlineData("웬지", "왠지")]
    [InlineData("할께", "할게")]
    [InlineData("역활", "역할")]
    [InlineData("되물림", "대물림")]
    [InlineData("임마", "인마")]
    public void Certain_규칙_감지(string word, string expectedSuggestion)
        => Engine().Check(word, null).Should()
            .Contain(x => x.Rule.Suggestion == expectedSuggestion && x.Rule.Level == Confidence.Certain);

    // ── 오탐 방지: 반드시 빈 결과 (완료 기준) ────────────────────
    [Theory]
    [InlineData("안녕하세요", null)]
    [InlineData("되고", null)]
    [InlineData("되면", null)]
    [InlineData("됩니다", "안")]
    [InlineData("않다", "하지")]     // '-지 않다' 정상
    [InlineData("안", null)]
    [InlineData("해야", null)]
    [InlineData("돼서", null)]        // '되어서'의 준말 = '돼서', 정상
    [InlineData("됐다", null)]        // '됐' 정상
    [InlineData("돼요", null)]        // '돼요' 정상
    [InlineData("하지", null)]
    [InlineData("된다", null)]
    public void 정상_문장_오탐_없음(string word, string? prev)
        => Engine().Check(word, prev).Should().BeEmpty();

    // ── Suspect: 문맥 의심 ───────────────────────────────────────
    [Fact]
    public void Suspect_문장끝_되는_돼_제안()
        => Engine().Check("되", null).Should()
            .Contain(x => x.Rule.Level == Confidence.Suspect && x.Rule.Suggestion == "돼");

    [Fact]
    public void Suspect_안_되는_안돼_제안()
        => Engine().Check("되", "안").Should().Contain(x => x.Rule.Suggestion == "안 돼");

    [Fact]
    public void Suspect_지_안은_않_제안()
        => Engine().Check("안", "하지").Should().Contain(x => x.Rule.Suggestion == "않");

    [Fact]
    public void Suspect_비활성화시_감지_안함()
        => Engine(o => o.EnableSuspect = false).Check("되", null).Should().BeEmpty();

    // ── Info: 기본 OFF ───────────────────────────────────────────
    [Fact]
    public void Info_기본_OFF_돼지_감지안함()
        => Engine().Check("돼지", null).Should().BeEmpty();

    [Fact]
    public void Info_활성화시_돼지_안내()
        => Engine(o => o.EnableInfo = true).Check("돼지", null).Should()
            .Contain(x => x.Rule.Level == Confidence.Info);

    // ── 개별 규칙 비활성화 ───────────────────────────────────────
    [Fact]
    public void 특정_규칙ID_비활성화()
    {
        var id = RuleSet.LoadDefault().Rules.First(r => r.Suggestion == "돼요").Id;
        Engine(o => o.DisabledRuleIds.Add(id)).Check("되요", null)
            .Should().NotContain(x => x.Rule.Suggestion == "돼요");
    }
}
