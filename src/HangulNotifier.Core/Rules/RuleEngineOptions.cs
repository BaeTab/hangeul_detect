namespace HangulNotifier.Core.Rules;

/// <summary>신뢰도별 활성화 및 개별 규칙 비활성화 설정. 설정 UI에서 제어.</summary>
public sealed class RuleEngineOptions
{
    public bool EnableCertain { get; set; } = true;
    public bool EnableSuspect { get; set; } = true;
    public bool EnableInfo { get; set; } = false;   // 기본 OFF
    public HashSet<string> DisabledRuleIds { get; set; } = new();

    public bool IsLevelEnabled(Confidence level) => level switch
    {
        Confidence.Certain => EnableCertain,
        Confidence.Suspect => EnableSuspect,
        Confidence.Info => EnableInfo,
        _ => false,
    };
}
