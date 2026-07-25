using System.Reflection;
using FluentAssertions;
using HangulNotifier.Core.Rules;

namespace HangulNotifier.Rules.Tests;

/// <summary>
/// 규칙 자가검증 게이트. 규칙에 붙은 examples/okExamples와 정상 코퍼스로
/// "감지돼야 할 것은 감지되고, 정상 표기는 절대 감지되지 않음"을 자동 검증한다.
/// 규칙을 추가하면 별도 테스트 없이 이 게이트가 검증한다.
/// </summary>
public class RuleSelfCheckTests
{
    // 규칙 수가 ~150개로 늘어나는 중이므로, 테스트마다(그리고 Theory 인스턴스마다) 매번
    // 임베디드 JSON을 리플렉션으로 재파싱하지 않도록 한 번만 로드해 캐시한다.
    private static readonly IReadOnlyList<Rule> AllRules = RuleSet.LoadDefault().Rules;

    /// <summary>전 신뢰도를 켠 엔진(Info 포함) — examples 검증용.</summary>
    private static RuleEngine AllLevelsEngine() =>
        new(AllRules, new RuleEngineOptions { EnableCertain = true, EnableSuspect = true, EnableInfo = true });

    /// <summary>기본 설정 엔진(Certain+Suspect, Info OFF) — 오탐 검증용.</summary>
    private static RuleEngine DefaultEngine() => new(AllRules, new RuleEngineOptions());

    public static TheoryData<string?> RulesWithExamples()
    {
        var data = new TheoryData<string?>();
        foreach (var r in AllRules.Where(r => r.Examples is { Count: > 0 }))
            data.Add(r.Id);
        // xUnit 2.x는 [Theory]+[MemberData]가 빈 TheoryData를 반환하면 0건 통과가 아니라
        // "No data found" 예외로 실패 처리한다. examples가 달린 규칙이 아직 없는 현재 단계에서
        // 게이트가 헛되이 실패하지 않도록 null placeholder 한 건을 넣어 우회한다.
        // (규칙에 examples가 추가되면 이 placeholder는 실제 ruleId들에 밀려 의미가 없어진다.)
        if (data.Count == 0) data.Add(null);
        return data;
    }

    [Theory]
    [MemberData(nameof(RulesWithExamples))]
    public void 규칙의_examples는_그_규칙에_감지된다(string? ruleId)
    {
        if (ruleId is null) return; // placeholder — examples가 달린 규칙이 아직 없음

        var rule = AllRules.First(r => r.Id == ruleId);
        var engine = AllLevelsEngine();

        foreach (var example in rule.Examples!)
        {
            var detections = engine.Check(example, null);
            detections.Should().Contain(d => d.Rule.Id == ruleId,
                because: $"규칙 '{ruleId}'의 예시 '{example}'은(는) 감지돼야 합니다");
        }
    }

    [Fact]
    public void 모든_규칙의_okExamples는_감지되지_않는다()
    {
        // okExamples는 어떤 레벨의 규칙에도 걸리면 안 되므로 Info까지 켜고 검사한다
        // (DefaultEngine은 Info가 꺼져 있어 Info 규칙의 오탐을 놓칠 수 있음).
        var engine = AllLevelsEngine();
        var failures = new List<string>();

        foreach (var rule in AllRules.Where(r => r.OkExamples is { Count: > 0 }))
        {
            foreach (var ok in rule.OkExamples!)
            {
                var detections = engine.Check(ok, null);
                foreach (var d in detections)
                    failures.Add($"'{ok}'(규칙 '{rule.Id}'의 정상 예시) → '{d.Rule.Id}'에 오탐");
            }
        }

        failures.Should().BeEmpty(because: "정상 표기는 어떤 규칙에도 걸리면 안 됩니다");
    }

    [Fact]
    public void 정상_코퍼스에서_오탐이_0건이다()
    {
        // 반드시 DefaultEngine(Info OFF) 유지: '돼지' 규칙 등 Info 레벨 규칙은
        // 맞춤법이 맞는 실제 단어도 "문맥 확인 유도" 목적으로 의도적으로 매칭한다.
        // Info를 켜고 이 코퍼스를 돌리면 설계상 실패가 발생하므로 절대 AllLevelsEngine으로 바꾸지 말 것.
        var engine = DefaultEngine();
        var failures = new List<string>();

        foreach (var line in LoadCorpus())
        {
            // 코퍼스 줄을 어절 단위로 검사(앱과 동일하게 어절 경계로 나눠 검사)
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? prev = null;
            foreach (var word in words)
            {
                var detections = engine.Check(word, prev);
                foreach (var d in detections)
                    failures.Add($"'{word}'(코퍼스: \"{line}\") → 규칙 '{d.Rule.Id}'에 오탐");
                prev = word;
            }
        }

        failures.Should().BeEmpty(because: "정상 한국어 코퍼스에서 감지가 발생하면 안 됩니다");
    }

    [Fact]
    public void 규칙_id는_중복되지_않는다()
    {
        var duplicates = AllRules.GroupBy(r => r.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty();
    }

    /// <summary>이관 전 51종의 id 전체. 이관 후에도 하나도 사라지면 안 된다.</summary>
    private static readonly string[] LegacyRuleIds =
    {
        "doeds", "doe-yo", "doe-seo", "doe-ya", "dwae-before-ending", "anh-an-dwae",
        "myeochil", "oraetman", "geumsae", "seolleim", "huian", "euieops", "waenman",
        "wenji", "halkke", "yeokhwal", "ittta", "doemullim", "imma",
        "dwaen", "dwaem", "dwael", "eotteoke", "waenil", "myeoch-jong", "yetnal",
        "bwaeyo", "damgwo", "jamgwo", "chireo", "orat-dongan", "tongjjaero", "jjagipgi",
        "nunsal", "gusiryeong", "neolbeureo", "nungop", "anieyo", "yukgaejang",
        "gopppaegi", "jaetteori", "geokkuro", "seolgeoji", "mureupsseu", "sseuregi",
        "dwae-sentence-end", "an-dwae", "anh-misuse", "ji-an",
        "dwaeji", "doege",
    };

    [Fact]
    public void 기존_규칙이_이관_후에도_모두_존재한다()
    {
        var ids = AllRules.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var missing = LegacyRuleIds.Where(id => !ids.Contains(id)).ToList();

        missing.Should().BeEmpty(because: "범주 파일 이관 중 규칙이 유실되면 안 됩니다");
    }

    [Fact]
    public void 모든_규칙은_필수_필드를_갖는다()
    {
        foreach (var rule in AllRules)
        {
            rule.Id.Should().NotBeNullOrWhiteSpace();
            rule.Pattern.Should().NotBeNullOrWhiteSpace(because: $"규칙 '{rule.Id}'");
            rule.Suggestion.Should().NotBeNullOrWhiteSpace(because: $"규칙 '{rule.Id}'");
            rule.Message.Should().NotBeNullOrWhiteSpace(because: $"규칙 '{rule.Id}'");
        }
    }

    [Fact]
    public void 모든_규칙의_정규식이_유효하다()
    {
        foreach (var rule in AllRules)
        {
            var act = () => System.Text.RegularExpressions.Regex.Match("테스트", rule.Pattern);
            act.Should().NotThrow(because: $"규칙 '{rule.Id}'의 pattern이 올바른 정규식이어야 합니다");
        }
    }

    private static IEnumerable<string> LoadCorpus()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("valid-korean.txt", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) yield return trimmed;
        }
    }
}
