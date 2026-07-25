using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevExpress.Xpf.Core;
using H.NotifyIcon;
using HangulNotifier.App.Configuration;
using HangulNotifier.App.Services;
using HangulNotifier.App.Views;
using HangulNotifier.Core.Rules;
using HangulNotifier.Data;
using HangulNotifier.Platform.Hooking;
using HangulNotifier.Platform.Ime;
using HangulNotifier.Platform.Security;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace HangulNotifier.App;

public partial class App : Application
{
    static App()
    {
        // DevExpress Win11 라이트 테마 (오버레이는 순수 WPF라 영향 없음)
        CompatibilitySettings.UseLightweightThemes = true;
        ApplicationThemeHelper.ApplicationThemeName = Theme.Win11Light.Name;
    }

    private ServiceProvider? _provider;
    private DetectionPipeline? _pipeline;
    private TaskbarIcon? _tray;
    private MenuItem? _pauseItem;
    private AppSettings _settings = new();
    private SettingsStore? _settingsStore;

    private StatisticsWindow? _statsWindow;
    private SettingsWindow? _settingsWindow;

    private UpdateChecker? _updateChecker;
    private DispatcherTimer? _updateTimer;
    private MenuItem? _downloadItem;
    private UpdateInfo? _pendingUpdate;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureRoot();

        bool diag = e.Args.Contains("--diag");

        var logConfig = new LoggerConfiguration();
        logConfig = diag ? logConfig.MinimumLevel.Debug() : logConfig.MinimumLevel.Information();
        Log.Logger = logConfig
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();

        SetupGlobalExceptionHandlers();
        Log.Information("HangulNotifier 시작");

        _settingsStore = new SettingsStore(AppPaths.SettingsFile);
        _settings = _settingsStore.Load();

        _provider = BuildServices(_settings);
        _pipeline = _provider.GetRequiredService<DetectionPipeline>();
        _pipeline.Diagnostics = diag;
        if (diag) Log.Information("진단 모드 활성화(--diag): 글자 내용은 기록하지 않음");

        BuildTray();
        _pipeline.Start();
        UpdatePauseMenu();

        // 업데이트 확인기(네트워크는 사용 안 함 — 켜져 있거나 수동 클릭 시에만 접속)
        _updateChecker = new UpdateChecker();
        RefreshUpdateChecks();

        if (e.Args.Contains("--test-overlay"))
            ShowTestOverlay();
        if (e.Args.Contains("--test-windows"))
        {
            ShowStatistics();
            ShowSettings();
        }
    }

    private void ShowTestOverlay()
    {
        var overlay = _provider!.GetRequiredService<OverlayService>();
        // 검증용 고정 위치(주 모니터 상단). 실제 동작은 캐럿 추적.
        overlay.Show(80, 80, "됬", "됐",
            "'됬'은 언제나 틀린 표기입니다. '되었다'의 준말은 '됐다'.",
            Core.Rules.Confidence.Certain, 60000);
    }

    private ServiceProvider BuildServices(AppSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddSingleton<Serilog.ILogger>(Log.Logger);
        services.AddSingleton(_settingsStore!);

        services.AddSingleton<KeyboardHook>();
        services.AddSingleton<ImeStateReader>();
        services.AddSingleton(_ => new SecureFieldDetector(settings.ExtraSecureProcesses));
        services.AddSingleton<IStatisticsRepository>(_ => new StatisticsRepository(AppPaths.StatsDb));
        services.AddSingleton(_ => BuildRuleEngine(settings));
        services.AddSingleton(_ => new OverlayService(Dispatcher));
        services.AddSingleton(_ => new NotificationSound(Dispatcher, Log.Logger));
        services.AddSingleton<DetectionPipeline>();

        return services.BuildServiceProvider();
    }

    private static RuleEngine BuildRuleEngine(AppSettings s)
    {
        var rules = RuleSet.LoadDefault().MergedWith(RuleSet.ParseUserFile(AppPaths.UserRulesFile));
        var opts = new RuleEngineOptions
        {
            EnableCertain = s.EnableCertain,
            EnableSuspect = s.EnableSuspect,
            EnableInfo = s.EnableInfo,
            DisabledRuleIds = new HashSet<string>(s.DisabledRuleIds),
        };
        return new RuleEngine(rules.Rules, opts);
    }

    private void BuildTray()
    {
        _tray = new TaskbarIcon
        {
            ToolTipText = "한글 맞춤법 실시간 알림기",
            Icon = TrayIcon.Load(),
            NoLeftClickDelay = true,
        };

        var menu = new ContextMenu();

        _pauseItem = new MenuItem { Header = "일시정지" };
        _pauseItem.Click += (_, _) => TogglePause();
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Separator());

        var statsItem = new MenuItem { Header = "통계 보기" };
        statsItem.Click += (_, _) => ShowStatistics();
        menu.Items.Add(statsItem);

        var settingsItem = new MenuItem { Header = "설정" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var updateItem = new MenuItem { Header = "업데이트 확인" };
        updateItem.Click += async (_, _) => await RunUpdateCheckAsync(manual: true);
        menu.Items.Add(updateItem);

        // 새 버전이 감지되면 표시되는 항목(평소 숨김)
        _downloadItem = new MenuItem { Header = "새 버전 다운로드", Visibility = Visibility.Collapsed };
        _downloadItem.Click += (_, _) =>
        {
            if (_pendingUpdate is not null) UpdateChecker.OpenReleasePage(_pendingUpdate.ReleaseUrl);
        };
        menu.Items.Add(_downloadItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "종료" };
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        _tray.ContextMenu = menu;
        _tray.TrayMouseDoubleClick += (_, _) => ShowStatistics();
        _tray.ForceCreate();
    }

    private void TogglePause()
    {
        if (_pipeline is null) return;
        if (_pipeline.IsPaused) _pipeline.Resume();
        else _pipeline.Pause();

        _settings.Paused = _pipeline.IsPaused;
        _settingsStore?.Save(_settings);
        UpdatePauseMenu();
    }

    private void UpdatePauseMenu()
    {
        if (_pauseItem is not null && _pipeline is not null)
            _pauseItem.Header = _pipeline.IsPaused ? "재개" : "일시정지";
    }

    private void ShowStatistics()
    {
        var repo = _provider!.GetRequiredService<IStatisticsRepository>();
        if (_statsWindow is null)
        {
            _statsWindow = new StatisticsWindow(repo);
            _statsWindow.Closed += (_, _) => _statsWindow = null;
        }
        _statsWindow.RefreshData();
        _statsWindow.Show();
        _statsWindow.Activate();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(
                _settings, _settingsStore!,
                _provider!.GetRequiredService<RuleEngine>(),
                _provider!.GetRequiredService<SecureFieldDetector>(),
                _provider!.GetRequiredService<IStatisticsRepository>(),
                _provider!.GetRequiredService<NotificationSound>());
            // 설정 닫힘 시 업데이트 확인 토글을 재반영(_settings 는 참조로 공유되어 저장 즉시 최신).
            _settingsWindow.Closed += (_, _) => { _settingsWindow = null; RefreshUpdateChecks(); };
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>설정에 따라 자동 업데이트 타이머를 시작/중지. 활성화 전환 시 즉시 1회 확인.</summary>
    private void RefreshUpdateChecks()
    {
        if (_settings.CheckForUpdates)
        {
            if (_updateTimer is null)
            {
                _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
                _updateTimer.Tick += async (_, _) => await RunUpdateCheckAsync(manual: false);
                _updateTimer.Start();
                _ = RunUpdateCheckAsync(manual: false); // 활성화 직후 1회
            }
        }
        else
        {
            _updateTimer?.Stop();
            _updateTimer = null;
        }
    }

    /// <summary>GitHub 릴리즈를 확인. 새 버전이면 트레이 알림 + 다운로드 메뉴 노출. 실패는 조용히 무시.</summary>
    private async Task RunUpdateCheckAsync(bool manual)
    {
        if (_updateChecker is null) return;

        var info = await _updateChecker.CheckAsync();
        if (info is not null)
        {
            _pendingUpdate = info;
            if (_downloadItem is not null)
            {
                _downloadItem.Header = $"새 버전 {info.Tag} 다운로드";
                _downloadItem.Visibility = Visibility.Visible;
            }
            _tray?.ShowNotification(
                "한글 맞춤법 알림기",
                $"새 버전 {info.Tag} 이(가) 있습니다. 트레이 메뉴 → '새 버전 다운로드'에서 받으세요.");
            Log.Information("새 버전 감지: {Tag}", info.Tag);
        }
        else if (manual)
        {
            _tray?.ShowNotification("한글 맞춤법 알림기", "현재 최신 버전입니다.");
        }
    }

    private void ExitApp()
    {
        Log.Information("종료 요청");
        Shutdown();
    }

    private void SetupGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "UI 스레드 미처리 예외");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error(args.ExceptionObject as Exception, "도메인 미처리 예외");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Task 미관측 예외");
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _updateTimer?.Stop();
            _updateChecker?.Dispose();
            _pipeline?.Dispose();
            (_provider?.GetService<IStatisticsRepository>() as IDisposable)?.Dispose();
            _tray?.Dispose();
            _provider?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "종료 정리 중 예외");
        }
        Log.Information("HangulNotifier 종료");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
