namespace HangulNotifier.Core.Rules;

/// <summary>규칙 신뢰도. Certain=무조건 오류, Suspect=문맥 의심, Info=판별 불가(기본 OFF).</summary>
public enum Confidence { Certain, Suspect, Info }

/// <summary>맞춤법 규칙.</summary>
/// <param name="Pattern">어절에 적용할 정규식.</param>
/// <param name="Suggestion">제안 표기.</param>
/// <param name="Message">사용자에게 보여줄 한 줄 설명.</param>
/// <param name="PreviousWordPattern">있으면 직전 어절이 이 정규식과 일치해야 발동.</param>
/// <param name="PreviousWordNotPattern">있으면 직전 어절이 이 정규식과 일치하면 발동하지 않음(직전 어절 없음은 통과).</param>
public sealed record Rule(
    string Id,
    string Pattern,
    string Suggestion,
    string Message,
    Confidence Level,
    string? PreviousWordPattern = null,
    string? PreviousWordNotPattern = null);

/// <summary>감지 결과.</summary>
public sealed record Detection(Rule Rule, string MatchedText, int Index);

public interface IRuleEngine
{
    IReadOnlyList<Detection> Check(string word, string? previousWord);
}
