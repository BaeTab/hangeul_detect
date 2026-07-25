using System.Collections.ObjectModel;
using System.Windows.Controls;
using DevExpress.Xpf.Core;
using HangulNotifier.App.Configuration;
using HangulNotifier.App.ViewModels;
using HangulNotifier.App.Services;
using HangulNotifier.Core.Rules;
using HangulNotifier.Data;
using HangulNotifier.Platform.Security;

namespace HangulNotifier.App.Views;

public partial class SettingsWindow : ThemedWindow
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly RuleEngine _engine;
    private readonly SecureFieldDetector _secure;
    private readonly IStatisticsRepository _stats;
    private readonly NotificationSound _sound;
    private readonly ObservableCollection<RuleToggle> _ruleToggles = new();

    public SettingsWindow(AppSettings settings, SettingsStore store, RuleEngine engine,
        SecureFieldDetector secure, IStatisticsRepository stats, NotificationSound sound)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;
        _engine = engine;
        _secure = secure;
        _stats = stats;
        _sound = sound;
        LoadIntoControls();
    }

    /// <summary>슬라이더 값 그대로 알림음을 들려준다(저장과 무관한 미리듣기).</summary>
    private void PreviewSound_Click(object sender, RoutedEventArgs e)
        => _sound.Play(VolumeSlider.Value);

    private void LoadIntoControls()
    {
        CertainCheck.IsChecked = _settings.EnableCertain;
        SuspectCheck.IsChecked = _settings.EnableSuspect;
        InfoCheck.IsChecked = _settings.EnableInfo;

        DurationSlider.Value = _settings.DisplayMs;
        PositionCombo.SelectedIndex = _settings.Position == PositionMode.BottomRight ? 1 : 0;

        SoundCheck.IsChecked = _settings.SoundEnabled;
        VolumeSlider.Value = _settings.SoundVolume;

        StartupCheck.IsChecked = _settings.StartWithWindows;

        UpdateCheck.IsChecked = _settings.CheckForUpdates;

        ExcludedBox.Text = string.Join(Environment.NewLine, _settings.ExcludedProcesses);
        WhitelistBox.Text = string.Join(Environment.NewLine, _settings.WhitelistWords);

        var disabled = new HashSet<string>(_settings.DisabledRuleIds);
        foreach (var r in RuleSet.LoadDefault().Rules)
        {
            _ruleToggles.Add(new RuleToggle
            {
                Id = r.Id,
                Label = $"[{Level(r.Level)}] {r.Suggestion}  ·  {Truncate(r.Message, 42)}",
                Enabled = !disabled.Contains(r.Id),
            });
        }
        RulesList.ItemsSource = _ruleToggles;
    }

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _settings.EnableCertain = CertainCheck.IsChecked ?? true;
        _settings.EnableSuspect = SuspectCheck.IsChecked ?? true;
        _settings.EnableInfo = InfoCheck.IsChecked ?? false;

        _settings.DisplayMs = (int)DurationSlider.Value;
        _settings.Position = PositionCombo.SelectedIndex == 1 ? PositionMode.BottomRight : PositionMode.Caret;

        _settings.SoundEnabled = SoundCheck.IsChecked ?? false;
        _settings.SoundVolume = VolumeSlider.Value;

        _settings.StartWithWindows = StartupCheck.IsChecked ?? false;

        _settings.CheckForUpdates = UpdateCheck.IsChecked ?? false;

        _settings.ExcludedProcesses = ExcludedBox.Text
            .Split('\n', '\r')
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

        // 사용자 사전은 어절을 그대로 저장(한글 — 소문자 변환하지 않음)
        _settings.WhitelistWords = WhitelistBox.Text
            .Split('\n', '\r')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

        _settings.DisabledRuleIds = _ruleToggles.Where(t => !t.Enabled).Select(t => t.Id).ToList();

        _store.Save(_settings);
        ApplyLive();
        Close();
    }

    private void ApplyLive()
    {
        _engine.Options = new RuleEngineOptions
        {
            EnableCertain = _settings.EnableCertain,
            EnableSuspect = _settings.EnableSuspect,
            EnableInfo = _settings.EnableInfo,
            DisabledRuleIds = new HashSet<string>(_settings.DisabledRuleIds),
        };
        StartupManager.Set(_settings.StartWithWindows);
        foreach (var p in _settings.ExtraSecureProcesses)
            _secure.AddBlacklisted(p);
    }

    private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void ClearStatsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "모든 통계 데이터를 삭제할까요? 되돌릴 수 없습니다.",
            "전체 삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            _stats.ClearAll();
    }

    private static string Level(Confidence c) => c switch
    {
        Confidence.Certain => "확실",
        Confidence.Suspect => "의심",
        _ => "참고",
    };

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
