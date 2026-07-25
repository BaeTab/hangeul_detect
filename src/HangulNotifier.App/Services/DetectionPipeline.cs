using HangulNotifier.App.Configuration;
using HangulNotifier.Core.Buffer;
using HangulNotifier.Core.Rules;
using HangulNotifier.Data;
using HangulNotifier.Platform.Caret;
using HangulNotifier.Platform.Hooking;
using HangulNotifier.Platform.Ime;
using HangulNotifier.Platform.Input;
using HangulNotifier.Platform.Security;
using HangulNotifier.Platform.Windowing;
using Serilog;

namespace HangulNotifier.App.Services;

/// <summary>
/// 감지 파이프라인(워커). 후킹 이벤트를 단일 스레드에서 소비한다.
///   게이트(제외/보안/IME) → VK 변환 → WordBuffer → RuleEngine → 쿨다운 → 오버레이/통계.
/// 후킹 콜백이 아니라 워커에서만 무거운 작업을 한다.
/// </summary>
public sealed class DetectionPipeline : IDisposable
{
    private const int TickMs = 100;

    private readonly KeyboardHook _hook;
    private readonly ImeStateReader _ime;
    private readonly SecureFieldDetector _secure;
    private readonly CaretLocator _caret = new();
    private readonly WordBuffer _buffer = new();
    private readonly RuleEngine _engine;
    private readonly DetectionCooldown _cooldown = new();
    private readonly OverlayService _overlay;
    private readonly IStatisticsRepository _stats;
    private readonly AppSettings _settings;
    private readonly ILogger _log;

    private CancellationTokenSource? _cts;
    private Task? _worker;

    // 상태
    private bool _ctrl, _alt;
    private IntPtr _lastForeground;
    private string? _currentProcess;
    private bool _currentExcluded;

    // 한/영 상태. 최신 TSF 앱은 교차 프로세스로 IME를 못 읽으므로, 전역 후킹이 본
    // 한/영 토글키(VK_HANGUL)로 로컬 추적한다. 확답 가능한 앱에선 IMM 값으로 재동기화.
    private const int VK_HANGUL = 0x15;
    private bool _assumedHangul = true;   // 기본 ON(한글 맞춤법기 특성상 한글 입력이 대부분)

    public bool IsPaused { get; private set; }

    /// <summary>진단 모드(--diag). 글자 내용은 절대 기록하지 않고 카운트/게이트 사유만 남긴다.</summary>
    public bool Diagnostics { get; set; }

    // 진단 카운터
    private long _diagKeyDowns, _diagCharsFed, _diagChecks, _diagMatches;
    private long _diagBlockExcluded, _diagBlockSecure, _diagBlockIme, _diagPass;
    private long _diagLastSummaryMs;
    private string _diagLastGate = "-";

    public DetectionPipeline(
        KeyboardHook hook, ImeStateReader ime, SecureFieldDetector secure,
        RuleEngine engine, OverlayService overlay, IStatisticsRepository stats,
        AppSettings settings, ILogger log)
    {
        _hook = hook;
        _ime = ime;
        _secure = secure;
        _engine = engine;
        _overlay = overlay;
        _stats = stats;
        _settings = settings;
        _log = log;

        _buffer.CheckRequested += OnCheckRequested;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => WorkerLoop(_cts.Token));
        if (!_settings.Paused)
            Resume();
        else
            IsPaused = true;
        _log.Information("DetectionPipeline 시작 (paused={Paused})", IsPaused);
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        _hook.Stop();                 // 후킹 자체 해제 (플래그 무시가 아님)
        _buffer.ForceReset();
        _overlay.HideNow();
        _log.Information("일시정지 — 후킹 해제");
    }

    public void Resume()
    {
        if (!IsPaused && _hook.IsRunning) return;
        IsPaused = false;
        _hook.Start();
        _log.Information("재개 — 후킹 설치");
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        var reader = _hook.Reader;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await WaitOrTick(reader, ct))
                {
                    while (reader.TryRead(out var ev)) ProcessEvent(ev);
                }
                _buffer.Tick(Environment.TickCount64);
                if (Diagnostics) DiagSummary(Environment.TickCount64);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error(ex, "워커 루프 예외");
        }
    }

    private static async Task<bool> WaitOrTick(System.Threading.Channels.ChannelReader<KeyEvent> reader, CancellationToken ct)
    {
        var wait = reader.WaitToReadAsync(ct).AsTask();
        var done = await Task.WhenAny(wait, Task.Delay(TickMs, ct)).ConfigureAwait(false);
        if (done == wait) return wait.Result;   // false = 채널 완료
        return true;                            // 타임아웃 → Tick만
    }

    private void ProcessEvent(KeyEvent ev)
    {
        int vk = ev.VirtualKeyCode;

        // 수식키 상태 추적 (Ctrl/Alt)
        if (IsCtrl(vk)) { _ctrl = ev.IsKeyDown; return; }
        if (IsAlt(vk)) { _alt = ev.IsKeyDown; return; }

        // 한/영 토글키: 로컬 한글 상태를 뒤집고 어절을 끊는다(모드 전환 = 경계).
        if (vk == VK_HANGUL && ev.IsKeyDown)
        {
            _assumedHangul = !_assumedHangul;
            _buffer.ForceReset();
            _overlay.HideNow();
            return;
        }

        if (!ev.IsKeyDown) return;   // 이후는 키다운만 처리

        if (Diagnostics) _diagKeyDowns++;

        if (!ForegroundOkAndHangul()) return;
        if (Diagnostics) { _diagPass++; _diagLastGate = "pass"; }

        // 단축키(Ctrl/Alt 조합)는 텍스트가 아니므로 어절을 끊는다
        if (_ctrl || _alt) { _buffer.ForceReset(); return; }

        var tk = KeyTranslator.Translate(vk, ev.ShiftDown);
        long now = ev.TimestampMs;
        switch (tk.Action)
        {
            case KeyAction.Character: if (Diagnostics) _diagCharsFed++; _buffer.FeedChar(tk.Character, now); break;
            case KeyAction.Backspace: _buffer.Backspace(now); break;
            case KeyAction.Boundary: _buffer.CommitBoundary(now); break;
            case KeyAction.Reset: _buffer.ForceReset(); _overlay.HideNow(); break;
            case KeyAction.None: default: break;
        }
    }

    /// <summary>포그라운드 게이트: 제외 프로세스/비밀번호/영문모드면 버퍼를 비우고 false.</summary>
    private bool ForegroundOkAndHangul()
    {
        var (hwnd, pid) = ForegroundInfo.Current();
        if (hwnd != _lastForeground)
        {
            _lastForeground = hwnd;
            _buffer.ForceReset();
            _overlay.HideNow();
            _currentProcess = ForegroundInfo.ProcessName(pid);
            _currentExcluded = IsExcluded(_currentProcess);
        }

        if (_currentExcluded) { if (Diagnostics) { _diagBlockExcluded++; _diagLastGate = "excluded"; } _buffer.ForceReset(); return false; }
        if (_secure.IsSecureContext()) { if (Diagnostics) { _diagBlockSecure++; _diagLastGate = "secure"; } _buffer.ForceReset(); return false; }

        // 한글 모드 판정: IMM이 확답하면(클래식 앱) 그 값으로 재동기화, 아니면 로컬 토글 추적값.
        // 미확정(TSF) 구간에서는 포그라운드 키보드 레이아웃이 '확실히 비한국어'면 감지하지 않는다
        // (Win+Space 로 영문 키보드로 바꾼 흔한 desync 방지). langId 를 못 읽으면 기존대로 통과(누락 방지).
        bool hangul;
        if (_ime.TryQueryDefinitive(out bool imm))
        {
            hangul = _assumedHangul = imm;
        }
        else if (_assumedHangul && _ime.ForegroundLayoutIsNonKorean())
        {
            if (Diagnostics) { _diagBlockIme++; _diagLastGate = "layout"; }
            _buffer.ForceReset();
            return false;
        }
        else
        {
            hangul = _assumedHangul;
        }

        if (!hangul) { if (Diagnostics) { _diagBlockIme++; _diagLastGate = "ime"; } _buffer.ForceReset(); return false; }
        return true;
    }

    /// <summary>진단 요약을 1초마다 로그로 남긴다(글자 내용 없음).</summary>
    private void DiagSummary(long nowMs)
    {
        if (nowMs - _diagLastSummaryMs < 1000) return;
        if (_diagKeyDowns == 0 && _diagPass == 0) { _diagLastSummaryMs = nowMs; return; }
        _diagLastSummaryMs = nowMs;

        var d = _ime.LastDiag;
        _log.Information(
            "[DIAG] keydown={KD} pass={Pass} charsFed={CF} checks={CK} matches={MT} | block(excl={BE},secure={BS},ime={BI}) lastGate={LG} | assumedHangul={AH} ime(imeWnd={Found},definitive={Def},open={Open},conv=0x{Conv:X},native={Nat},lang=0x{Lang:X}) proc={Proc}",
            _diagKeyDowns, _diagPass, _diagCharsFed, _diagChecks, _diagMatches,
            _diagBlockExcluded, _diagBlockSecure, _diagBlockIme, _diagLastGate,
            _assumedHangul, d.ImeWndFound, d.Definitive, d.Open, d.ConvMode, d.NativeOn, d.LangId, _currentProcess ?? "-");
    }

    private bool IsExcluded(string? process)
    {
        if (string.IsNullOrEmpty(process)) return false;
        foreach (var e in _settings.ExcludedProcesses)
            if (!string.IsNullOrWhiteSpace(e) && process.Contains(e.ToLowerInvariant(), StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>사용자 사전에 등록된 어절과 정확히 일치하면 true(대소문자 구분, 조사 붙은 형태는 별도 등록 필요).</summary>
    private bool IsWhitelisted(string word)
    {
        var list = _settings.WhitelistWords;
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i], word, StringComparison.Ordinal))
                return true;
        return false;
    }

    private void OnCheckRequested(WordCheck wc)
    {
        if (Diagnostics) _diagChecks++;
        var detections = _engine.Check(wc.Word, wc.PreviousWord);
        if (detections.Count == 0) return;

        // 사용자 사전(화이트리스트): 사용자가 '맞다'고 등록한 어절은 오탐으로 보고 알림·통계 모두 건너뛴다.
        if (IsWhitelisted(wc.Word)) return;

        if (Diagnostics) _diagMatches++;

        // 가장 심각한 것 하나만 (Certain < Suspect < Info)
        var best = detections.OrderBy(d => (int)d.Rule.Level).First();

        long now = Environment.TickCount64;
        if (!_cooldown.ShouldNotify(wc.Word, best.Rule.Id, now)) return;

        var loc = _settings.Position == PositionMode.Caret ? _caret.Locate() : _caret.Corner();
        _overlay.Show(loc.X, loc.Y, best.MatchedText, best.Rule.Suggestion, best.Rule.Message, best.Rule.Level, _settings.DisplayMs);

        try { _stats.Record(best.Rule.Id, _currentProcess, DateTimeOffset.Now); }
        catch (Exception ex) { _log.Warning(ex, "통계 기록 실패"); }

        if (_settings.SoundEnabled)
            try { System.Media.SystemSounds.Asterisk.Play(); } catch { /* 무음 실패 무시 */ }

        // 로그에는 규칙ID와 시각만 (입력 텍스트 절대 금지)
        _log.Debug("감지 rule={RuleId} level={Level}", best.Rule.Id, best.Rule.Level);
    }

    private static bool IsCtrl(int vk) => vk is 0x11 or 0xA2 or 0xA3;
    private static bool IsAlt(int vk) => vk is 0x12 or 0xA4 or 0xA5;

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _hook.Stop();
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch { /* 종료 중 예외 무시 */ }
        _cts?.Dispose();
    }
}
