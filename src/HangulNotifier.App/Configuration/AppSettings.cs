namespace HangulNotifier.App.Configuration;

public enum PositionMode { Caret, BottomRight }

/// <summary>사용자 설정. %APPDATA%\HangulNotifier\settings.json 에 저장.</summary>
public sealed class AppSettings
{
    // 신뢰도별 알림
    public bool EnableCertain { get; set; } = true;
    public bool EnableSuspect { get; set; } = true;
    public bool EnableInfo { get; set; } = false;

    // 표시
    public int DisplayMs { get; set; } = 1500;
    public PositionMode Position { get; set; } = PositionMode.Caret;

    // 사운드 (기본 OFF)
    public bool SoundEnabled { get; set; } = false;
    public double SoundVolume { get; set; } = 0.5;

    // 자동 시작 (HKCU Run)
    public bool StartWithWindows { get; set; } = false;

    // 일시정지 상태(재시작 시 복원)
    public bool Paused { get; set; } = false;

    // 감지 제외 프로세스 (기본: 게임/원격/가상머신)
    public List<string> ExcludedProcesses { get; set; } = new()
    {
        // 안티치트 게임
        "valorant", "valorant-win64-shipping", "leagueclient",
        // 원격 데스크톱
        "mstsc", "teamviewer", "anydesk", "rustdesk",
        // 가상머신
        "vmware", "vmware-vmx", "virtualbox", "vmwareunity",
    };

    // 비밀번호 감지 추가 프로세스(보안 블랙리스트에 더함)
    public List<string> ExtraSecureProcesses { get; set; } = new();

    // 개별 비활성화 규칙 ID
    public List<string> DisabledRuleIds { get; set; } = new();

    // 업데이트 확인 (옵트인, 기본 OFF — 켤 때만 GitHub 릴리즈에 접속해 버전만 조회)
    public bool CheckForUpdates { get; set; } = false;

    // 사용자 사전(화이트리스트): 여기 등록된 어절과 '정확히 일치'하면 오탐으로 보고 알림/통계를 건너뛴다.
    public List<string> WhitelistWords { get; set; } = new();
}
