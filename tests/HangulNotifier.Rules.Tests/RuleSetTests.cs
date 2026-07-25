using System.Text.RegularExpressions;
using FluentAssertions;
using HangulNotifier.Core.Rules;

namespace HangulNotifier.Rules.Tests;

public class RuleSetTests
{
    [Fact]
    public void 기본_규칙_임베드_로드()
    {
        var rs = RuleSet.LoadDefault();
        rs.Rules.Should().NotBeEmpty();
        rs.Rules.Should().Contain(r => r.Level == Confidence.Certain);
        rs.Rules.Should().Contain(r => r.Level == Confidence.Suspect);
        rs.Rules.Should().Contain(r => r.Level == Confidence.Info);
    }

    [Fact]
    public void 모든_규칙_ID는_고유()
    {
        var ids = RuleSet.LoadDefault().Rules.Select(r => r.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void 모든_규칙_정규식_유효()
    {
        foreach (var r in RuleSet.LoadDefault().Rules)
        {
            var act = () => Regex.Match("테스트문장", r.Pattern);
            act.Should().NotThrow($"규칙 {r.Id} 패턴이 유효해야 함");
        }
    }

    [Fact]
    public void 사용자_규칙_병합_ID충돌시_덮어쓰기()
    {
        var baseSet = RuleSet.LoadDefault();
        var existingId = baseSet.Rules.First().Id;
        var overridden = new Rule(existingId, "무언가", "제안X", "설명X", Confidence.Certain);
        var merged = baseSet.MergedWith(new[] { overridden });
        merged.Rules.Count(r => r.Id == existingId).Should().Be(1);
        merged.Rules.First(r => r.Id == existingId).Suggestion.Should().Be("제안X");
    }

    [Fact]
    public void 규칙_JSON의_examples와_okExamples를_파싱한다()
    {
        const string json = """
            [
              {
                "id": "test-rule",
                "pattern": "메세지",
                "suggestion": "메시지",
                "message": "'메시지'가 바른 표기입니다.",
                "level": "Certain",
                "examples": ["메세지", "메세지를"],
                "okExamples": ["메시지", "메신저"]
              }
            ]
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var rules = RuleSet.Parse(stream);

        rules.Should().ContainSingle();
        rules[0].Examples.Should().BeEquivalentTo("메세지", "메세지를");
        rules[0].OkExamples.Should().BeEquivalentTo("메시지", "메신저");
    }

    [Fact]
    public void examples가_없는_규칙은_null이다()
    {
        const string json = """
            [
              {
                "id": "no-examples",
                "pattern": "됬",
                "suggestion": "됐",
                "message": "설명",
                "level": "Certain"
              }
            ]
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var rules = RuleSet.Parse(stream);

        rules[0].Examples.Should().BeNull();
        rules[0].OkExamples.Should().BeNull();
    }
}
