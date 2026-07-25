# 한글 맞춤법 실시간 알림기 — 설계 문서

**작성일:** 2026-07-24
**상태:** v0.1.0 코드 완료 (Phase 0~5 전부). 실기기 타이핑 현장 검증 진행 중.
**타깃:** Windows 데스크톱, .NET 8 (`net8.0-windows`), WPF 트레이 상주 앱

---

## 1. 제품 정의

Windows에서 **어떤 프로그램에 타이핑하든** 한글 맞춤법 오류를 감지해 **캐럿 근처에 짧은 알림을 띄우는** 백그라운드 상주 프로그램.

### 절대 원칙 (위반 금지)

1. **텍스트를 수정하지 않는다.** 자동 교정 없음. 감지 → 알림만.
2. **포커스를 뺏지 않는다.** 오버레이가 활성화되면 IME 조합이 끊긴다. 최우선 제약.
3. **입력 데이터는 외부로 나가지 않는다.** 네트워크 코드 없음. 로컬 온리.
4. **비밀번호 입력은 감지 자체를 하지 않는다.** 버퍼링도 안 함.

---

## 2. 기술 스택 (확정)

| 항목 | 선택 |
|---|---|
| 런타임 | .NET 8 (`net8.0-windows`) |
| UI | WPF + **DevExpress WPF** 컴포넌트 (사용자 지시) |
| MVVM | **DevExpress.Mvvm** + `DevExpress.Mvvm.CodeGenerators` (`[GenerateViewModel]`/`[GenerateProperty]`/`[GenerateCommand]`) |
| 테마 | `DevExpress.Wpf.ThemesLW` (Win11Light) |
| 차트/그리드 | `DevExpress.Wpf` (`ChartControl`, `GridControl`) — 통계 대시보드 |
| 트레이 | `H.NotifyIcon.Wpf` (DevExpress에 트레이 컴포넌트 없음) |
| DB | `Microsoft.Data.Sqlite` (통계 전용) |
| DI/호스팅 | `Microsoft.Extensions.Hosting` |
| 로깅 | `Serilog` (롤링 파일 싱크) |
| 설정 | `System.Text.Json`, `%APPDATA%\HangulNotifier\settings.json` |
| 테스트 | `xUnit` + `FluentAssertions` |

- **DevExpress 버전:** 로컬 피드(`C:\Program Files\DevExpress 25.1`) 25.1.12 확인. 복원 가능.
- **CommunityToolkit.Mvvm 미사용:** 전역 CLAUDE.md 기본 규칙이나, 이 프로젝트는 사용자가 DevExpress MVVM을 명시 지시 → 오버라이드.
- 외부 API 호출 라이브러리는 추가하지 않는다. 네트워크 의존성 0.

**환경 확인:** .NET SDK 9.0.316(net8.0-windows 타깃 가능) + WindowsDesktop 8.0 런타임 설치됨.

---

## 3. 솔루션 구조

기존 `hangeul/` DevExpress 데모 스캐폴드(Customer 그리드 데모)는 폐기하고, 아래 다중 프로젝트 구조를 `D:\myrepo\BaeTab\hangeul\`에 새로 만든다. 솔루션명 `HangulNotifier.sln`.

```
HangulNotifier.sln
├─ src/
│  ├─ HangulNotifier.Core/            # net8.0 — Win32/WPF/DevExpress 의존 0, 100% 테스트 가능
│  │  ├─ Hangul/HangulJamo.cs         # 자모 상수 테이블(초/중/종성 인덱스)
│  │  ├─ Hangul/HangulAutomata.cs     # 두벌식 조합 오토마타
│  │  ├─ Buffer/WordBuffer.cs         # 어절 버퍼 + 트리거/쿨다운
│  │  ├─ Rules/RuleSet.cs             # 규칙 로더(JSON)
│  │  ├─ Rules/RuleEngine.cs          # IRuleEngine 구현
│  │  ├─ Rules/Detection.cs           # Rule/Detection/Confidence 레코드
│  │  └─ rules/*.json                 # 외부화된 규칙 세트
│  ├─ HangulNotifier.Platform/        # net8.0-windows — 모든 P/Invoke 격리 (DevExpress 없음)
│  │  ├─ Hooking/KeyboardHook.cs
│  │  ├─ Ime/ImeStateReader.cs
│  │  ├─ Caret/CaretLocator.cs
│  │  ├─ Security/SecureFieldDetector.cs
│  │  └─ Native/*.cs                  # DllImport 선언 모음
│  ├─ HangulNotifier.Data/            # net8.0 — SQLite 통계 저장소 (DevExpress 없음)
│  │  └─ StatisticsRepository.cs
│  └─ HangulNotifier.App/             # net8.0-windows, WinExe — WPF 진입점 (DevExpress 사용)
│     ├─ App.xaml(.cs)                # DI 호스트, Serilog, 전역 예외 처리, DevExpress 테마
│     ├─ Tray/…                       # H.NotifyIcon 트레이
│     ├─ Views/OverlayWindow.xaml(.cs)# ★ 순수 WPF 클릭-스루 (DevExpress ThemedWindow 아님)
│     ├─ Views/SettingsWindow.xaml    # DevExpress 에디터
│     ├─ Views/StatisticsWindow.xaml  # DevExpress ChartControl + GridControl
│     ├─ ViewModels/*                 # DevExpress.Mvvm.CodeGenerators
│     ├─ Services/                    # 워커 파이프라인, 오케스트레이션
│     └─ app.manifest                 # asInvoker, PerMonitorV2 DPI
└─ tests/
   ├─ HangulNotifier.Core.Tests/      # 오토마타 + 버퍼
   └─ HangulNotifier.Rules.Tests/     # 규칙 정확도 + 오탐 방지 스위트
```

### 의존성 규칙 (설계의 핵심)

```
Core        ← (아무것도 참조 안 함, Win32/WPF/DevExpress 없음)
Platform    → Core
Data        → Core
App         → Core, Platform, Data  (+ DevExpress, H.NotifyIcon)
Core.Tests  → Core
Rules.Tests → Core
```

`Core`가 순수하므로 오토마타·규칙 엔진을 문자열 입력만으로 전부 단위 테스트한다. DevExpress는 **App 계층에만** 존재 → 테스트 대상 로직은 상용 의존성과 무관.

---

## 4. 스레딩 모델 (안정성의 핵심)

```
[WH_KEYBOARD_LL 후킹 콜백]  ── 10ms 이내 반환, CallNextHookEx 통과 ──►
      │ KeyEvent 를 Channel<KeyEvent> 에 write (그 외 작업 금지)
      ▼
[워커 스레드]  Channel 소비
      │ 1) SecureFieldDetector / ImeStateReader 게이트
      │ 2) HangulAutomata.Feed → 음절 복원
      │ 3) WordBuffer 어절 확정/디바운스
      │ 4) RuleEngine.Check → Detection
      │ 5) StatisticsRepository 기록 (규칙ID+시각+프로세스명만)
      ▼
[UI 스레드 marshal]  Dispatcher.InvokeAsync
      │ 재사용 OverlayWindow 내용 교체 + Show() (Activate 금지)
```

- 후킹 콜백 안에서 **정규식/DB/파일 IO/UI 조작 금지**.
- 델리게이트를 static 필드로 유지해 GC 수거 방지.
- 일시정지 = 후킹 자체 해제(`UnhookWindowsHookEx`).

---

## 5. 모듈 명세 요약 (원본 프롬프트 §4 채택)

- **KeyboardHook:** `SetWindowsHookEx(WH_KEYBOARD_LL=13)`(비주입형), 콜백 10ms 이내, static 델리게이트, 항상 `CallNextHookEx`, `Dispose`에서 언훅. `KeyEvent(VirtualKeyCode, IsKeyDown, ShiftDown, TimestampMs)`.
- **ImeStateReader:** 포그라운드 창 `GetKeyboardLayout` LANGID `0x0412` + IME `IME_CMODE_NATIVE`. 200ms 캐시. 영문 모드면 버퍼 비우고 스킵.
- **HangulAutomata:** 두벌식 키맵 raw 가상키코드 → 완성형. `0xAC00 + (초성×21+중성)×28+종성`. 복합 중성/종성, **종성 이월**, Backspace 되돌리기. API: `Committed/Composing/Current/Feed/Backspace/Reset`.
- **WordBuffer:** 확정 트리거(공백/Enter/Tab/문장부호), 400ms 디바운스, 강제 리셋(창변경/클릭/방향키/30초), 직전 어절 보관, 64자 초과 리셋, `(어절,규칙ID)` 5초 쿨다운.
- **RuleEngine:** `rules/*.json` + `%APPDATA%\...\user-rules.json`. `RegexOptions.Compiled`. 신뢰도별(Certain/Suspect/Info) 제어. `IRuleEngine.Check(word, previousWord)`.
- **CaretLocator:** ① `GetGUIThreadInfo` rcCaret→ClientToScreen, ② UIA `TextPattern`(별도 스레드 150ms 타임아웃), ③ 마우스/우하단 폴백. PerMonitorV2, `WorkingArea` 클램프.
- **OverlayWindow(★):** `WindowStyle=None`, `AllowsTransparency=True`, `ShowActivated=False`, `Focusable=False`, `IsHitTestVisible=False`. `SourceInitialized`에서 `WS_EX_NOACTIVATE|TRANSPARENT|TOOLWINDOW`. `Show()`만, `Activate/Focus/ShowDialog` 금지. 인스턴스 재사용, 1.5초, 페이드 120/200ms, 최신으로 교체. 신뢰도별 좌측 색상바(Certain 빨강/Suspect 노랑/Info 회색). **DevExpress ThemedWindow 사용 안 함.**
- **SecureFieldDetector:** 3중 차단 — ① `ES_PASSWORD(0x0020)`, ② UIA `IsPasswordProperty`, ③ 프로세스 블랙리스트(암호관리자/보안SW/사용자 추가). 버퍼 평문 최대 64자, 로그에 입력 내용 절대 미기록.
- **Statistics(Data):** SQLite `%APPDATA%\HangulNotifier\stats.db`. `detections(id, rule_id, detected_at INTEGER, process)`. **입력 텍스트/창 제목/경로 저장 안 함.** 집계는 앱단에서 로컬 타임존 경계로 계산.
- **Tray & Settings:** 일시정지·재개 / 통계 / 설정 / 종료. 시작프로그램(HKCU Run), 신뢰도별 ON/OFF, 표시시간·위치모드, 사운드, 제외 프로세스, 규칙 개별 ON/OFF, 통계 삭제.
- **통계 대시보드(DevExpress):** `GridControl`(TOP10 규칙별 횟수) + `ChartControl`(최근 30일 일별 막대) + 오늘/주/월 카운트 카드 + 전체 삭제.

### 규칙 세트
원본 §4.5 Certain/Suspect/Info 표를 `rules/*.json`으로 외부화. 되/돼 판정은 `돼=되+어` 원리를 메시지에 일관 노출(하/해 대입 검사법 포함).

---

## 6. 백신 오탐 최소화 (코드 서명 없음) ★신규 제약

유료 코드 서명 인증서가 없으므로, 전역 키보드 후킹을 쓰는 앱이 백신/SmartScreen에 최대한 덜 걸리도록 아래를 전부 적용한다.

**행위 기반 (가장 중요 — 실제 키로거와의 결정적 차이):**
- 키 입력을 **파일/DB/네트워크에 절대 저장하지 않는다.** (핵심 원칙 3·4와 동일) — 키로거 판정의 1순위 신호를 원천 제거.
- **네트워크 코드 0.** 소켓/HTTP 라이브러리 자체를 참조하지 않는다.
- **프로세스 주입/메모리 읽기 없음.** `WH_KEYBOARD_LL`은 비주입형(전역 DLL 주입형 `WH_KEYBOARD` 아님). `SendInput`/`keybd_event` 미사용.
- 관리자 권한 미요구(`asInvoker`) — 키 감시 앱의 권한 상승 요청은 대표적 위험 신호.

**바이너리/메타데이터 기반 (휴리스틱 회피):**
- 완전한 어셈블리 메타데이터: `Company=Bae Hyunwoo`, `Product`, `FileVersion`/`AssemblyVersion`, `Description`, `Copyright`, 앱 아이콘. (메타데이터 공란은 휴리스틱 플래그)
- 게시: **프레임워크 종속** 단일 파일(`SelfContained=false`, `PublishSingleFile=true`) + **압축 비활성화**(`EnableCompressionInSingleFile=false`). 패킹/압축 실행 파일은 휴리스틱 트리거.
- 난독화·패킹 도구 사용 안 함(투명한 코드가 유리).
- `app.manifest`에 정식 애플리케이션 식별 정보 포함.

**신뢰 확보 (배포 단계):**
- README에 "전역 키보드 후킹을 사용하지만 입력을 저장/전송하지 않는다"를 명시, 동작 원리 공개.
- 오픈소스 공개로 소스 검증 경로 제공.
- SmartScreen 최초 실행 경고는 서명 없이는 불가피 → README에 "추가 정보 → 실행" 안내.
- Inno Setup 인스톨러도 동일하게 메타데이터/버전 정보 채움.
- (자가서명 인증서는 평판 이점이 없어 적용하지 않음. 향후 유료 EV 인증서 확보 시 서명 단계만 추가.)

---

## 7. 빌드 순서 (자율 진행 — 사용자 지시)

체크포인트 중단 없이 끝까지 진행하되, 각 Phase는 빌드+테스트 통과를 내부 게이트로 삼는다.

- **Phase 0 — 세팅:** 구 스캐폴드 제거, `HangulNotifier.sln` + 6개 프로젝트 생성, DevExpress/패키지 추가, 참조 배선, 빈 빌드 통과.
- **Phase 1 — Core:** `HangulAutomata`→`WordBuffer`→`RuleEngine`. TDD. 규칙 정확도 전부 확정.
- **Phase 2 — Platform:** `KeyboardHook`→`ImeStateReader`→`SecureFieldDetector`.
- **Phase 3 — Overlay:** `CaretLocator`→`OverlayWindow`. 포커스 유지/캐럿 위치.
- **Phase 4 — 통합:** 트레이·설정·통계(DevExpress)·자동 시작·워커 파이프라인.
- **Phase 5 — 마감:** 단일 파일 게시(AV 하드닝 포함), `asInvoker`, Inno Setup 인스톨러, README/CHANGELOG.

---

## 8. 하지 말 것 (원본 §6 채택)

- ❌ `SendInput` 텍스트 주입 · ❌ 후킹 콜백 내 UI/파일IO/DB/정규식 · ❌ 오버레이 `Activate/Focus/ShowDialog` · ❌ 네트워크 요청 · ❌ 입력 텍스트를 로그/DB에 기록 · ❌ 관리자 권한 요구 · ❌ 입력 소비(`CallNextHookEx` 미호출)

---

## 9. 완료 기준 (원본 §7 채택) — 현황

자동 검증 완료:
- [x] 정상 문장 오탐 0 — 규칙 테스트 42개(오탐 방지 스위트 포함) 통과.
- [x] `HangulAutomata` 커버리지 90%+ — **100%** 달성.
- [x] 로그에 입력 텍스트 0글자 — 로그는 규칙ID·시각·창 위치만 기록(입력 텍스트 미기록).

코드 구현 완료 + 실기기 부분 검증:
- [x] 앱 구동·전역 후킹 설치 성공(로그 확인), 오버레이/통계/설정 창 렌더링(PrintWindow 확인), 게시 단일 파일 exe 실행.
- [x] 콜백 최소화(Channel 적재만) — 콜백 평균 1ms 미만 설계. 일시정지=후킹 해제 구현.

사용자 현장 검증 필요(타이핑 필요, 자동화 불가):
- [ ] 메모장/Word/Chrome/VS Code/카카오톡에서 `되요` 감지.
- [ ] 알림 중 IME 조합 미중단(`한글되요` 끊김 없이 입력).
- [ ] 비번란 알림 없음. [ ] 8시간 상주 후 메모리 +20MB 미만.

---

## 10. 알려진 제약 (README 명시)

- 관리자 권한 앱(작업관리자/regedit)에서는 동작 안 함(Windows 보안 정책).
- 안티치트 게임에서 차단/종료 가능 → 게임 프로세스 기본 제외.
- 백신이 전역 후킹을 키로거로 오탐 가능 → §6 하드닝 + 오픈소스로 완화(서명 없음).
- 일부 UWP/Store 앱은 캐럿 위치 획득 불가 → 고정 위치 폴백.

---

## 11. 이 환경에서의 조정 사항

- **에이전트 팀:** 전역 CLAUDE.md의 `ecc:*` 에이전트 부재 → 가용 에이전트(Plan/Explore/general-purpose) + 적대적 리뷰 패스로 대체, 투입 시 명시.
- **Git:** 신규 `git init`. 커밋은 전역 규칙(본문 한글, prefix 영어, **AI 서명 없음**).
- **브랜딩:** 개인 개발자 명의(`Bae Hyunwoo`)로 통일. 조직·지역 표기는 넣지 않는다.
- **오버라이드 기록:** MVVM 프레임워크는 사용자 지시로 CommunityToolkit → DevExpress.Mvvm.
