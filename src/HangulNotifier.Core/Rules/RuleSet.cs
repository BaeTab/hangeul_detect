using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HangulNotifier.Core.Rules;

/// <summary>
/// 규칙 모음. 기본 규칙은 어셈블리 임베드 리소스(rules/*.json)에서, 사용자 규칙은 파일에서 로드한다.
/// </summary>
public sealed class RuleSet
{
    public IReadOnlyList<Rule> Rules { get; }

    public RuleSet(IReadOnlyList<Rule> rules) => Rules = rules;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>임베드된 기본 규칙 전체를 로드.</summary>
    public static RuleSet LoadDefault()
    {
        var asm = typeof(RuleSet).Assembly;
        var rules = new List<Rule>();
        foreach (var name in asm.GetManifestResourceNames()
                     .Where(n => n.Contains(".rules.", StringComparison.OrdinalIgnoreCase)
                                 && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) continue;
            rules.AddRange(Parse(s));
        }
        return new RuleSet(rules);
    }

    /// <summary>사용자 규칙 파일(%APPDATA%…\user-rules.json)을 로드. 없거나 깨졌으면 빈 목록.</summary>
    public static IReadOnlyList<Rule> ParseUserFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return Array.Empty<Rule>();
            using var s = File.OpenRead(path);
            return Parse(s);
        }
        catch
        {
            return Array.Empty<Rule>();  // 사용자 규칙 오류가 앱을 죽이지 않게
        }
    }

    public static IReadOnlyList<Rule> Parse(Stream json)
        => JsonSerializer.Deserialize<List<RuleDto>>(json, JsonOpts)
               ?.Select(d => d.ToRule()).ToList()
           ?? new List<Rule>();

    /// <summary>추가 규칙을 병합. 같은 Id는 나중 것으로 덮어쓴다.</summary>
    public RuleSet MergedWith(IEnumerable<Rule> extra)
    {
        var map = new Dictionary<string, Rule>(StringComparer.Ordinal);
        foreach (var r in Rules) map[r.Id] = r;
        foreach (var r in extra) map[r.Id] = r;
        return new RuleSet(map.Values.ToList());
    }

    private sealed class RuleDto
    {
        public string Id { get; set; } = "";
        public string Pattern { get; set; } = "";
        public string Suggestion { get; set; } = "";
        public string Message { get; set; } = "";
        public Confidence Level { get; set; } = Confidence.Certain;
        public string? PreviousWordPattern { get; set; }
        public string? PreviousWordNotPattern { get; set; }
        public List<string>? Examples { get; set; }
        public List<string>? OkExamples { get; set; }

        public Rule ToRule() => new(Id, Pattern, Suggestion, Message, Level,
            PreviousWordPattern, PreviousWordNotPattern, Examples, OkExamples);
    }
}
