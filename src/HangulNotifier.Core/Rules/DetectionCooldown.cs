namespace HangulNotifier.Core.Rules;

/// <summary>
/// (어절, 규칙ID) 조합에 대한 중복 알림 방지 쿨다운. 기본 5초.
/// 순수 로직 — 시간은 nowMs로 주입한다.
/// </summary>
public sealed class DetectionCooldown
{
    public const long WindowMs = 5000;

    private readonly Dictionary<(string word, string ruleId), long> _last = new();

    /// <summary>이 (어절,규칙) 조합을 지금 알려도 되는가? 되면 시각을 기록하고 true.</summary>
    public bool ShouldNotify(string word, string ruleId, long nowMs)
    {
        var key = (word, ruleId);
        if (_last.TryGetValue(key, out var t) && nowMs - t < WindowMs)
            return false;
        _last[key] = nowMs;
        return true;
    }

    public void Clear() => _last.Clear();
}
