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
    // ── 확장 규칙 ─────────────────────────────────────────────────
    [InlineData("됀다", "된")]
    [InlineData("안됌", "됨")]
    [InlineData("됄까", "될")]
    [InlineData("어떻해", "어떡해")]
    [InlineData("왠일", "웬일")]
    [InlineData("몆월", "몇")]
    [InlineData("옜날", "옛")]
    [InlineData("뵈요", "봬요")]
    [InlineData("담궈", "담가")]
    [InlineData("잠궈", "잠가")]
    [InlineData("치뤄", "치러")]
    [InlineData("오랜동안", "오랫동안")]
    [InlineData("통채로", "통째로")]
    [InlineData("짜집기", "짜깁기")]
    [InlineData("눈쌀", "눈살")]
    [InlineData("궁시렁", "구시렁")]
    [InlineData("눈꼽", "눈곱")]
    [InlineData("아니예요", "아니에요")]
    [InlineData("육계장", "육개장")]
    [InlineData("곱배기", "곱빼기")]
    [InlineData("재털이", "재떨이")]
    [InlineData("꺼꾸로", "거꾸로")]
    [InlineData("설겆이", "설거지")]
    [InlineData("쓰래기", "쓰레기")]
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
    // ── 확장 규칙 오탐 방지: 올바른 표기는 걸리지 않아야 함 ──────
    [InlineData("된장", null)]        // '됀'과 혼동 금지
    [InlineData("됨", null)]          // 명사형 정상
    [InlineData("될까", null)]
    [InlineData("몇월", null)]        // '몆'과 혼동 금지
    [InlineData("옛날", null)]        // '옜'과 혼동 금지
    [InlineData("봬요", null)]        // 정상
    [InlineData("담가", null)]        // 정상('담그다')
    [InlineData("잠가", null)]        // 정상('잠그다')
    [InlineData("치러", null)]        // 정상('치르다')
    [InlineData("오랫동안", null)]    // 정상(오랫만 규칙과 혼동 금지)
    [InlineData("통째로", null)]
    [InlineData("눈살", null)]
    [InlineData("눈곱", null)]
    [InlineData("아니에요", null)]
    [InlineData("친구예요", null)]    // 받침 없는 명사 뒤 '-예요' 정상
    [InlineData("육개장", null)]
    [InlineData("설거지", null)]
    [InlineData("쓰레기", null)]
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
