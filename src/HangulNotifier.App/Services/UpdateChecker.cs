using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace HangulNotifier.App.Services;

/// <summary>감지된 새 버전 정보.</summary>
public sealed record UpdateInfo(Version Version, string Tag, string ReleaseUrl);

/// <summary>
/// GitHub 릴리즈 기반 <b>옵트인</b> 업데이트 확인기.
///
/// 설계 원칙 (백신 하드닝 · 프라이버시 유지):
/// - 기본 비활성. 설정에서 켜거나 사용자가 직접 "업데이트 확인"을 누를 때만 네트워크 사용.
/// - GitHub Releases API로 <b>버전 문자열만</b> 조회한다. 입력·통계 등 사용자 데이터는 절대 전송하지 않는다.
/// - 인앱 다운로드·실행 없음. 새 버전이면 릴리즈 페이지를 기본 브라우저로 열어 사용자가 직접 설치한다.
/// - 모든 예외는 삼켜 로깅만 한다(감지 파이프라인/앱 안정성에 영향 금지).
/// - 신규 서드파티 의존성 없음(HttpClient·System.Text.Json 은 .NET 8 기본 제공).
/// </summary>
public sealed class UpdateChecker : IDisposable
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/BaeTab/hangeul_detect/releases/latest";
    private const string ReleasesPageFallback =
        "https://github.com/BaeTab/hangeul_detect/releases/latest";

    private readonly HttpClient _http;
    private readonly Version _current;

    public UpdateChecker()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub API 는 User-Agent 를 요구한다.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("HangulNotifier-UpdateChecker");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _current = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));
    }

    /// <summary>최신 릴리즈가 현재 버전보다 높으면 <see cref="UpdateInfo"/>, 아니면 null. 실패해도 null(예외 없음).</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(LatestReleaseApi, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Debug("업데이트 확인 응답 실패: HTTP {Status}", (int)resp.StatusCode);
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer
                .DeserializeAsync<GithubRelease>(stream, cancellationToken: ct)
                .ConfigureAwait(false);

            if (release?.TagName is null || release.Draft) return null;
            if (!TryParseVersion(release.TagName, out var latest)) return null;
            if (latest <= _current) return null;

            var url = string.IsNullOrWhiteSpace(release.HtmlUrl) ? ReleasesPageFallback : release.HtmlUrl!;
            return new UpdateInfo(latest, release.TagName!, url);
        }
        catch (Exception ex)
        {
            // 네트워크 오류/타임아웃/취소/파싱 실패 — 조용히 무시.
            Log.Debug(ex, "업데이트 확인 중 예외(무시)");
            return null;
        }
    }

    /// <summary>릴리즈 페이지를 기본 브라우저로 연다.</summary>
    public static void OpenReleasePage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "릴리즈 페이지 열기 실패: {Url}", url);
        }
    }

    /// <summary>"v0.3.0", "0.3.0", "0.3.0-beta", "0.3" 등을 파싱. 실패 시 false.</summary>
    internal static bool TryParseVersion(string tag, out Version version)
    {
        version = new Version(0, 0);
        var s = tag.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];

        // 앞쪽의 [숫자.] 부분만 취한다 (예: "0.3.0-beta" -> "0.3.0").
        int i = 0;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
        s = s[..i].Trim('.');
        if (s.Length == 0) return false;
        if (!s.Contains('.')) s += ".0"; // "1" -> "1.0"

        if (!Version.TryParse(s, out var parsed) || parsed is null) return false;
        version = Normalize(parsed);
        return true;
    }

    // Build/Revision 미지정(-1) 잡음을 없애기 위해 Major.Minor.Build(>=0)로 정규화.
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    public void Dispose() => _http.Dispose();

    private sealed class GithubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    }
}
