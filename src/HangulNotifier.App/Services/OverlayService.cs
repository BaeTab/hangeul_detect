using System.Windows.Media;
using System.Windows.Threading;
using HangulNotifier.App.Views;
using HangulNotifier.Core.Rules;

namespace HangulNotifier.App.Services;

/// <summary>
/// 오버레이 창을 재사용하며 알림을 띄운다. 워커 스레드에서 호출되므로 UI 스레드로 마샬링한다.
/// 신뢰도별 좌측 색상 바: Certain 빨강 / Suspect 노랑 / Info 회색.
/// </summary>
public sealed class OverlayService
{
    private static readonly Brush CertainBrush = Frozen("#E53935");
    private static readonly Brush SuspectBrush = Frozen("#FFB300");
    private static readonly Brush InfoBrush = Frozen("#9E9E9E");

    private readonly Dispatcher _ui;
    private OverlayWindow? _window;

    public OverlayService(Dispatcher ui) => _ui = ui;

    /// <summary>캐럿 물리 픽셀 좌표(px, py) 근처에 감지 알림을 표시.</summary>
    public void Show(double px, double py, string wrong, string suggestion, string message, Confidence level, int displayMs)
    {
        var accent = level switch
        {
            Confidence.Certain => CertainBrush,
            Confidence.Suspect => SuspectBrush,
            _ => InfoBrush,
        };
        _ui.InvokeAsync(() =>
        {
            _window ??= new OverlayWindow();
            _window.ShowNotification(px, py, wrong, suggestion, message, accent, displayMs);
        });
    }

    public void HideNow() => _ui.InvokeAsync(() => _window?.HideNow());

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
