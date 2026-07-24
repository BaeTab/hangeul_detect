using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using HangulNotifier.Platform.Windowing;

namespace HangulNotifier.App.Views;

/// <summary>
/// 캐럿 근처에 뜨는 클릭-스루 알림 오버레이. 인스턴스를 재사용하고 내용만 교체한다.
/// 표시는 Show()만 사용. Activate/Focus/ShowDialog 절대 호출하지 않는다.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int DefaultDisplayMs = 1500;
    private const int FadeInMs = 120;
    private const int FadeOutMs = 200;

    private readonly DispatcherTimer _dismissTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DefaultDisplayMs) };
        _dismissTimer.Tick += (_, _) => { _dismissTimer.Stop(); FadeOut(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 클릭 통과 + 비활성화 확장 스타일
        var hwnd = new WindowInteropHelper(this).Handle;
        OverlayInterop.MakeClickThroughNoActivate(hwnd);
    }

    /// <summary>물리 픽셀 좌표(px,py: 캐럿 하단) 근처에 알림을 표시. 연속 호출은 최신으로 교체.</summary>
    public void ShowNotification(double px, double py, string wrong, string suggestion, string message, Brush accent, int displayMs)
    {
        WrongRun.Text = wrong;
        SuggestRun.Text = suggestion;
        MessageLine.Text = message;
        AccentBar.Background = accent;
        _dismissTimer.Interval = TimeSpan.FromMilliseconds(displayMs > 0 ? displayMs : DefaultDisplayMs);

        if (!IsVisible)
            Show();          // 절대 Activate/Focus 하지 않음
        UpdateLayout();      // SizeToContent 반영 후 위치 지정

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            OverlayInterop.MoveNoActivate(hwnd, (int)px, (int)(py + 4));

        Serilog.Log.Debug("오버레이 표시 pos=({X},{Y}) size=({W}x{H})",
            (int)px, (int)(py + 4), (int)ActualWidth, (int)ActualHeight);

        // 페이드 인 + 자동 소멸 타이머 재시작(교체)
        BeginAnimation(OpacityProperty, null);
        Opacity = Math.Max(Opacity, 0.0);
        BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 1.0, TimeSpan.FromMilliseconds(FadeInMs)));

        _dismissTimer.Stop();
        _dismissTimer.Start();
    }

    /// <summary>즉시 숨김(일시정지/포커스 변경 등).</summary>
    public void HideNow()
    {
        _dismissTimer.Stop();
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        if (IsVisible) Hide();
    }

    private void FadeOut()
    {
        var anim = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(FadeOutMs));
        anim.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            if (IsVisible) Hide();
        };
        BeginAnimation(OpacityProperty, anim);
    }
}
