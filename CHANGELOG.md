# Changelog

한글 맞춤법 실시간 알림기 **HangulNotifier**의 모든 변경 사항을 기록합니다.

버전 관리는 [Semantic Versioning](https://semver.org/lang/ko/) 을 따릅니다.

---

## [0.2.0] - 2026-07-25

### Fixed
- **[핵심] 최신 앱에서 감지가 전혀 되지 않던 문제 수정.** 원인: 포그라운드(다른 프로세스) 창의 IME 한/영 상태를 레거시 IMM32 API(`GetKeyboardLayout`/`ImmGetContext`/`ImmGetDefaultIMEWnd`)로 읽으려 했으나, 이들은 자기 프로세스 창에서만 유효해 크롬·카카오톡·UWP·Visual Studio 등 TSF 기반 앱에서 항상 0을 반환 → 모든 키 입력이 IME 게이트에서 차단됨.
- 해결: 전역 후킹이 직접 관찰하는 한/영 토글키(`VK_HANGUL`)로 한/영 상태를 로컬 추적(기본 ON)하고, 클래식 앱에서는 IMM이 확답을 줄 때 그 값으로 재동기화. (`AttachThreadInput` 방식은 IME 조합을 방해할 수 있어 채택하지 않음)

### Added
- 맞춤법 규칙 **25종 → 51종**으로 확장 (Certain 45 · Suspect 4 · Info 2). 추가 예: `됀→된`, `됌→됨`, `됄→될`, `어떻해→어떡해`, `왠일→웬일`, `담궈→담가`, `잠궈→잠가`, `치뤄→치러`, `오랜동안→오랫동안`, `통채로→통째로`, `짜집기→짜깁기`, `눈쌀→눈살`, `궁시렁→구시렁`, `눈꼽→눈곱`, `아니예요→아니에요`, `육계장→육개장`, `곱배기→곱빼기`, `재털이→재떨이`, `꺼꾸로→거꾸로`, `설겆이→설거지`, `쓰래기→쓰레기`, `뵈요→봬요` 등.
- 진단 모드(`--diag` 실행 인자): 어느 단계에서 감지가 막히는지 로그로 확인. **글자 내용은 기록하지 않고** 카운트·게이트 사유만 남기는 프라이버시 안전 모드.
- 단위 테스트 97개 → **139개**(신규 규칙 감지 + 오탐 방지 쌍).

### Changed
- 버전 0.1.0 → 0.2.0.

---

## [0.1.0] - 2026-07-24

### Added

- **Core 맞춤법 엔진**
  - 두벌식 한글 오토마타: 종성 이월, 겹받침 처리 완전 구현
  - 어절 단위 입력 버퍼: 실시간 조합 중 단어 경계 자동 감지
  - 규칙 엔진: 되/돼, 몇일/며칠, 띄어쓰기 등 기본 규칙 25종(Certain 19 + Suspect 4 + Info 2) + 사용자 규칙 확장(JSON), 정규식 기반
  - 신뢰도 분류: Certain (확실), Suspect (의심), Info (정보성)
  - 단위 테스트 97개 작성, 오토마타 커버리지 100%

- **Platform 계층 (Windows API)**
  - 전역 키보드 후킹 (`WH_KEYBOARD_LL`): 비주입형, 관리자 권한 불필요
  - IME 상태 판정: 한글/영문 모드 자동 감지
  - 비밀번호/보안 입력 3중 차단
    - Win32 `ES_PASSWORD` 스타일(Edit 컨트롤)
    - UI Automation `IsPassword` (웹/브라우저 비밀번호 필드)
    - 프로세스 블랙리스트(비밀번호 관리자·국내 보안/은행 플러그인 + 사용자 추가)
  - 캐럿(텍스트 커서) 위치 3단계 폴백 추적(GetGUIThreadInfo → UIA TextPattern → 마우스/고정)
  - 멀티 모니터 작업영역 클램프

- **오버레이 UI**
  - 클릭-스루 알림 창: 입력 포커스 유지, 입력 중단 없음
  - 신뢰도별 색상 표시 (🔴 Certain, 🟡 Suspect, ⚪ Info)
  - 캐럿 추적 팝업: 오류 위치 정확 표시

- **통합 기능 (WPF + DevExpress)**
  - 시스템 트레이 상주: 일시정지/재개/통계/설정/종료
  - 설정 UI (DevExpress)
    - 신뢰도 수준별 알림 ON/OFF 제어
    - 감지 표시 위치, 음성 알림, 시작 시 자동 실행 설정
    - 프로세스 제외 목록 관리
    - 규칙 활성화 선택
  - 통계 대시보드 (DevExpress ChartControl/GridControl)
    - 오늘/이번 주/이번 달 감지 횟수 카드
    - 자주 틀리는 맞춤법 TOP 10
    - 최근 30일 일별 추이 막대 그래프, 전체 삭제
  - SQLite 통계 저장소 (`stats.db`)
    - 입력 텍스트 미저장 (규칙ID, 타임스탬프, 프로세스명만 기록)
  - Serilog 롤링 로그: 일별 파일, 최근 7일 유지

- **배포 및 설치**
  - 단일 파일 게시 (무압축): `HangulNotifier.exe` (~115MB) + `e_sqlite3.dll`
  - Inno Setup 6 인스톨러
    - Per-User 설치 (관리자 권한 불필요)
    - .NET 8 Desktop Runtime 자동 확인 및 설치 유도
    - 시작 메뉴 바로가기, 제거 기능 포함
  - 백신 하드닝: 코드 서명 인증서 없음 (오픈소스 공개로 검증 가능)

- **보안 및 개인정보**
  - 입력 내용 저장 안 함 (로컬 메모리에서만 처리, 즉시 폐기)
  - 네트워크 통신 코드 0
  - 프로세스 메모리 주입 없음
  - 비밀번호 필드 입력 감시 차단
  - 로그 파일에 사용자 입력 미기록

---

## [Unreleased]

(추가 예정)

---

## 참조

- **GitHub Repository**: [BaeTab/hangeul_detect](https://github.com/BaeTab/hangeul_detect)
- **.NET 8**: [Microsoft .NET](https://dotnet.microsoft.com/)
- **WPF**: [Windows Presentation Foundation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- **DevExpress**: [DevExpress Controls for WPF](https://www.devexpress.com/products/net/controls/wpf/)
- **Inno Setup**: [jrsoftware.org](https://jrsoftware.org/)
