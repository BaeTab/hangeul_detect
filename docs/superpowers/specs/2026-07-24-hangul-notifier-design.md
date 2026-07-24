# 한글 맞춤법 실시간 알림기 — 설계 문서

**작성일:** 2026-07-24
**상태:** 승인 대기
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
| UI | WPF |
| 트레이 | `H.NotifyIcon.Wpf` |
| DB | `Microsoft.Data.Sqlite` (통계 전용) |
| DI/호스팅 | `Microsoft.Extensions.Hosting` |
| MVVM | `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) |
| 로깅 | `Serilog` (롤링 파일 싱크) |
| 설정 | `System.Text.Json`, `%APPDATA%\HangulNotifier\settings.json` |
| 테스트 | `xUnit` + `FluentAssertions` |

외부 API 호출 라이브러리는 추가하지 않는다. 네트워크 의존성 0.

**환경 확인 완료:** .NET SDK 9.0.316(net8.0-windows 타깃 가능) + WindowsDesktop 8.0 런타임 설치됨.

---

## 3. 솔루션 구조

기존 `hangeul/` DevExpress 데모 스캐폴드는 폐기하고(사용자 승인 완료), 아래 다중 프로젝트 구조를 `D:\myrepo\BaeTab\hangeul\`에 새로 만든다. 솔루션명은 `HangulNotifier.sln`.

```
HangulNotifier.sln
├─ src/
│  ├─ HangulNotifier.Core/            # net8.0 — Win32/WPF 의존 0, 100% 테스트 가능
│  │  ├─ Hangul/HangulJamo.cs         # 자모 상수 테이블(초/중/종성 인덱스)
│  │  ├─ Hangul/HangulAutomata.cs     # 두벌식 조합 오토마타
│  │  ├─ Buffer/WordBuffer.cs         # 어절 버퍼 + 트리거/쿨다운
│  │  ├─ Rules/RuleSet.cs             # 규칙 로더(JSON)
│  │  ├─ Rules/RuleEngine.cs          # IRuleEngine 구현
│  │  ├─ Rules/Detection.cs           # Rule/Detection/Confidence 레코드
│  │  └─ rules/*.json                 # 외부화된 규칙 세트
│  ├─ HangulNotifier.Platform/        # net8.0-windows — 모든 P/Invoke 격리
│  │  ├─ Hooking/KeyboardHook.cs
│  │  ├─ Ime/ImeStateReader.cs
│  │  ├─ Caret/CaretLocator.cs
│  │  ├─ Security/SecureFieldDetector.cs
│  │  └─ Native/*.cs                  # DllImport 선언 모음
│  ├─ HangulNotifier.Data/            # net8.0 — SQLite 통계 저장소
│  │  └─ StatisticsRepository.cs
│  └─ HangulNotifier.App/             # net8.0-windows, WinExe — WPF 진입점
│     ├─ App.xaml(.cs)                # DI 호스트, Serilog, 전역 예외 처리
│     ├─ Tray/TrayIconViewModel.cs
│     ├─ Views/OverlayWindow.xaml(.cs)
│     ├─ Views/SettingsView.xaml
│     ├─ Views/StatisticsView.xaml
│     ├─ ViewModels/*
│     ├─ Services/                    # 워커 파이프라인, 오케스트레이션
│     └─ app.manifest                 # asInvoker, PerMonitorV2 DPI
└─ tests/
   ├─ HangulNotifier.Core.Tests/      # 오토마타 + 버퍼
   └─ HangulNotifier.Rules.Tests/     # 규칙 정확도 + 오탐 방지 스위트
```

### 의존성 규칙 (설계의 핵심)

```
Core        ← (아무것도 참조 안 함, Win32/WPF 없음)
Platform    → Core
Data        → Core
App         → Core, Platform, Data
Core.Tests  → Core
Rules.Tests → Core
```

`Core`가 순수하기 때문에 오토마타와 규칙 엔진을 문자열 입력만으로 콘솔/단위 테스트에서 전부 검증할 수 있다. 완료 기준(오토마타 90% 커버리지, 정상 문장 오탐 0)이 이 경계에 의존한다.

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
      │ 5) StatisticsRepository 기록 (규칙ID+시각만)
      ▼
[UI 스레드 marshal]  Dispatcher.InvokeAsync
      │ 재사용 OverlayWindow 내용 교체 + Show() (Activate 금지)
```

- 후킹 콜백 안에서 **정규식/DB/파일 IO/UI 조작 금지** (§명세 6).
- 델리게이트를 static 필드로 유지해 GC 수거 방지(후킹 조용히 죽는 버그 예방).
- 일시정지 = 후킹 자체 해제(`UnhookWindowsHookEx`). 플래그 무시 아님.

---

## 5. 모듈 명세 요약

원본 프롬프트 §4 명세를 그대로 채택한다. 핵심만 요약:

- **KeyboardHook:** `SetWindowsHookEx(WH_KEYBOARD_LL=13)`, 콜백 10ms 이내, static 델리게이트, 항상 `CallNextHookEx`, `Dispose`에서 언훅. `KeyEvent(VirtualKeyCode, IsKeyDown, ShiftDown, TimestampMs)`.
- **ImeStateReader:** 포그라운드 창의 `GetKeyboardLayout` LANGID `0x0412` 확인 + IME `IME_CMODE_NATIVE`. 200ms 캐시. 영문 모드면 버퍼 비우고 스킵.
- **HangulAutomata:** 두벌식 키맵으로 raw 가상키코드 → 완성형 음절. 유니코드 `0xAC00 + (초성×21 + 중성)×28 + 종성`. 복합 중성/종성 결합, **종성 이월**, Backspace 되돌리기. API: `Committed`, `Composing`, `Current`, `Feed`, `Backspace`, `Reset`.
- **WordBuffer:** 어절 단위 수집. 확정 트리거(공백/Enter/Tab/문장부호), 400ms 디바운스, 강제 리셋(창 변경/클릭/방향키/30초 무입력), 직전 어절 1개 보관, 64자 초과 리셋, `(어절,규칙ID)` 5초 쿨다운.
- **RuleEngine:** `rules/*.json` 로드 + `%APPDATA%\HangulNotifier\user-rules.json`. `RegexOptions.Compiled` 사전 컴파일. 신뢰도별(Certain/Suspect/Info) 활성화 제어. `IRuleEngine.Check(word, previousWord)`.
- **CaretLocator:** 3단계 폴백 — ① `GetGUIThreadInfo` rcCaret → ClientToScreen, ② UI Automation `TextPattern`(별도 스레드 150ms 타임아웃), ③ 마우스 오프셋/우하단 고정. PerMonitorV2 DPI, `WorkingArea` 클램프.
- **OverlayWindow:** `WS_EX_NOACTIVATE|TRANSPARENT|TOOLWINDOW`, `ShowActivated=False`, `Focusable=False`, `IsHitTestVisible=False`. `Show()`만, `Activate/Focus/ShowDialog` 금지. 인스턴스 재사용, 1.5초 표시, 페이드 120/200ms, 최신 알림으로 교체. 신뢰도별 좌측 색상바.
- **SecureFieldDetector:** 3중 차단 — ① `ES_PASSWORD(0x0020)`, ② UIA `IsPasswordProperty`, ③ 프로세스 블랙리스트(암호관리자/보안SW/사용자 추가). 버퍼 평문 최대 64자, 로그에 입력 내용 절대 미기록.
- **Statistics (Data):** SQLite `%APPDATA%\HangulNotifier\stats.db`. `detections(id, rule_id, detected_at, process)`. **입력 텍스트/창 제목/경로 저장 안 함.** 대시보드: 오늘/주/월 횟수, TOP10, 30일 추이, 전체 삭제.
- **Tray & Settings:** 일시정지·재개 / 통계 / 설정 / 종료. 시작프로그램(HKCU Run), 신뢰도별 ON/OFF, 표시시간·위치모드, 사운드, 제외 프로세스, 규칙 개별 ON/OFF, 통계 삭제.

### 규칙 세트

원본 §4.5의 Certain/Suspect/Info 표를 `rules/*.json`으로 외부화. 되/돼 판정은 `돼 = 되 + 어` 원리를 메시지에 일관 노출(하/해 대입 검사법 포함). 하드코딩 금지.

---

## 6. 테스트 전략

- **Core는 TDD.** 오토마타/규칙은 원본이 요구 케이스를 이미 명시 → 실패 테스트 먼저 작성 후 구현.
  - 오토마타 필수: `되`,`돼`,`됐`,`않`,`앉`,`읽`,`밟`,`의`,`쐐`, 종성 이월(`않아`), 겹받침 이월(`읽어`), Backspace.
  - 규칙 오탐 방지 스위트: `안녕하세요`,`되고`,`되면`,`안 됩니다`,`하지 않다` 등 정상 문장에서 감지 0.
- 목표: `HangulAutomata` 단위 테스트 커버리지 90%+.
- Platform/App은 수동 검증(메모장/Word/Chrome/VS Code/카카오톡) — 포커스 유지·캐럿 위치·비번란 차단 확인.

---

## 7. 빌드 순서 (Phase 체크포인트)

각 Phase는 빌드+테스트 통과 후 사용자 확인을 거쳐 다음으로 진행한다(체크포인트 방식 확정).

- **Phase 0 — 세팅:** 구 스캐폴드 제거, `HangulNotifier.sln` + 6개 프로젝트 생성, 패키지 추가, 참조 배선, 빈 빌드 통과.
- **Phase 1 — Core:** `HangulAutomata` → `WordBuffer` → `RuleEngine`. TDD. 콘솔 하네스로 문자열→감지 검증. **규칙 정확도 전부 여기서 확정.**
- **Phase 2 — Platform:** `KeyboardHook` → `ImeStateReader` → `SecureFieldDetector`. 콘솔에 감지 출력.
- **Phase 3 — Overlay:** `CaretLocator` → `OverlayWindow`. 실제 앱들에서 포커스 유지/캐럿 위치 확인.
- **Phase 4 — 통합:** 트레이, 설정, 통계, 자동 시작.
- **Phase 5 — 마감:** 단일 파일 게시(`PublishSingleFile`, `SelfContained=false`), `asInvoker` 매니페스트, Inno Setup 인스톨러, README/CHANGELOG.

---

## 8. 하지 말 것 (원본 §6 채택)

- ❌ `SendInput` 텍스트 주입(교정 기능 자체 금지)
- ❌ 후킹 콜백 내 UI/파일IO/DB/정규식
- ❌ 오버레이 `Activate/Focus/ShowDialog`
- ❌ 네트워크 요청
- ❌ 입력 텍스트를 로그/DB에 기록
- ❌ 관리자 권한 요구(매니페스트 `asInvoker`)
- ❌ 입력 소비(`CallNextHookEx` 미호출)

---

## 9. 완료 기준 (원본 §7 채택)

- [ ] 메모장/Word/Chrome 주소창/Chrome 텍스트영역/VS Code/카카오톡에서 `되요` 입력 시 알림.
- [ ] 알림 중 IME 조합 미중단(`한글되요` 끊김 없이 입력).
- [ ] 정상 문장 오탐 0.
- [ ] 비번란에서 알림 안 뜸.
- [ ] 8시간 상주 후 메모리 증가 20MB 미만, 후킹 생존.
- [ ] 후킹 콜백 평균 1ms 미만.
- [ ] `HangulAutomata` 커버리지 90%+.
- [ ] 일시정지→재개 정상.
- [ ] 로그에 입력 텍스트 0글자.

---

## 10. 알려진 제약 (README 명시)

- 관리자 권한 앱(작업관리자/regedit)에서는 동작 안 함(Windows 보안 정책, 우회 안 함).
- 안티치트 게임에서 차단/종료 가능 → 게임 프로세스 기본 제외.
- 백신이 전역 후킹을 키로거로 오탐 가능 → 코드 서명 + 오픈소스 공개로 신뢰 확보.
- 일부 UWP/Store 앱은 캐럿 위치 획득 불가 → 고정 위치 폴백.

---

## 11. 이 환경에서의 조정 사항

- **에이전트 팀:** 전역 CLAUDE.md의 `ecc:*`(architect/security-reviewer/silent-failure-hunter) 에이전트가 이 환경에 없음 → 구현 단계에서 가용 에이전트(Plan/Explore/general-purpose)와 적대적 리뷰 패스로 대체하며, 투입 시 명시한다.
- **Git:** 폴더를 신규 `git init`. 커밋은 전역 규칙 준수(본문 한글, prefix 영어, **AI 서명 없음**).
- **브랜딩:** 개인 앱(`com.baetab.*` 계열)이므로 H.Soft 주체성 유지.
