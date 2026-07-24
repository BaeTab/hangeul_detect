using System.Text.RegularExpressions;

namespace HangulNotifier.Core.Rules;

/// <summary>
/// 어절을 규칙과 대조해 감지 결과를 만든다. 정규식은 기동 시 Compiled로 미리 컴파일.
/// 순수 로직 — UI/Win32 의존 없음.
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private sealed record CompiledRule(Rule Rule, Regex Pattern, Regex? Prev, Regex? PrevNot);

    private readonly List<CompiledRule> _rules;

    /// <summary>활성화 설정. 런타임에 교체 가능(설정 변경 즉시 반영).</summary>
    public RuleEngineOptions Options { get; set; }

    public RuleEngine(IEnumerable<Rule> rules, RuleEngineOptions? options = null)
    {
        Options = options ?? new RuleEngineOptions();
        _rules = rules.Select(Compile).ToList();
    }

    private static CompiledRule Compile(Rule r)
    {
        const RegexOptions o = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        return new CompiledRule(
            r,
            new Regex(r.Pattern, o),
            r.PreviousWordPattern is null ? null : new Regex(r.PreviousWordPattern, o),
            r.PreviousWordNotPattern is null ? null : new Regex(r.PreviousWordNotPattern, o));
    }

    public IReadOnlyList<Detection> Check(string word, string? previousWord)
    {
        var result = new List<Detection>();
        if (string.IsNullOrEmpty(word)) return result;

        foreach (var cr in _rules)
        {
            if (!Options.IsLevelEnabled(cr.Rule.Level)) continue;
            if (Options.DisabledRuleIds.Contains(cr.Rule.Id)) continue;

            // 직전 어절이 반드시 일치해야 하는 조건
            if (cr.Prev is not null && (previousWord is null || !cr.Prev.IsMatch(previousWord)))
                continue;
            // 직전 어절이 일치하면 발동하지 않는 조건 (없으면 통과)
            if (cr.PrevNot is not null && previousWord is not null && cr.PrevNot.IsMatch(previousWord))
                continue;

            var m = cr.Pattern.Match(word);
            if (m.Success)
                result.Add(new Detection(cr.Rule, m.Value, m.Index));
        }
        return result;
    }
}
