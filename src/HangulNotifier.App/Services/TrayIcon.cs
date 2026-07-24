using System.Diagnostics;
using DrawingIcon = System.Drawing.Icon;

namespace HangulNotifier.App.Services;

/// <summary>트레이 아이콘 로드. 실행 파일에 임베드된 앱 아이콘을 사용하고, 실패 시 시스템 기본.</summary>
public static class TrayIcon
{
    public static DrawingIcon? Load()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                var ico = DrawingIcon.ExtractAssociatedIcon(exe);
                if (ico is not null) return ico;
            }
        }
        catch
        {
            // 무시하고 폴백
        }
        return System.Drawing.SystemIcons.Application;
    }
}
