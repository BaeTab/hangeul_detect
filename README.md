# HangulNotifier

**한글 맞춤법 실시간 알림기** — Windows 백그라운드 상주 앱  
어떤 프로그램에 타이핑해도 한글 맞춤법 오류를 캐럿 옆에 즉시 알려줍니다.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/en-us/windows)
[![UI](https://img.shields.io/badge/UI-WPF-0078D4)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Status](https://img.shields.io/badge/status-Development%20(Phase%202)-yellow)](#-개발-현황)

---

## ✨ 소개

**HangulNotifier**는 Windows에서 실행되는 백그라운드 앱입니다. 메모장, Word, 웹 브라우저, VS Code 등 **어떤 프로그램에서든** 한글을 입력할 때, 맞춤법 오류를 감지하고 **캐럿(텍스트 커서) 근처에 팝업 알림으로 띄워줍니다.**

- ✅ **자동 교정 없음** — 오류를 지적하기만 하고, 텍스트를 수정하지 않습니다.
- ✅ **포커스 유지** — 알림이 뜨면서 입력 포커스를 빼앗지 않아 입력 중단이 없습니다.
- ✅ **트레이 상주** — 백그라운드에서 조용히 실행되며, 언제든 일시정지·재개 가능합니다.

---

## 🔒 개인정보 & 보안

**가장 중요한 약속:**

1. **입력 내용을 저장하지 않습니다**
   - 타이핑한 텍스트를 파일, 데이터베이스, 클라우드 어디에도 저장하지 않습니다.
   - 로컬 메모리에서만 처리하고 즉시 버립니다.
   - 통계 저장소(`stats.db`)에는 규칙ID와 감지 시각, 프로세스명만 기록합니다. **입력 텍스트 자체는 절대 저장하지 않습니다.**

2. **네트워크 전송 없음**
   - 네트워크 관련 라이브러리(HTTP 클라이언트, 소켓 등)를 사용하지 않습니다.
   - 서버 통신 0. 모든 처리가 당신의 PC 안에서만 일어납니다.

3. **비밀번호 입력란 감지 안 함**
   - 비밀번호 필드(`<input type="password">` 등)에서는 입력 감지 자체를 하지 않습니다.
   - 감시 중단 + 입력 버퍼링 안 함.

4. **로그에 입력 텍스트 없음**
   - 앱 로그 파일(`%APPDATA%\HangulNotifier\logs\`)에 사용자 입력 내용을 기록하지 않습니다.

### "키로거가 아닌 이유"

이 앱은 **전역 키보드 후킹**을 사용하므로, 초기에 "키로거" 의심을 받을 수 있습니다. 하지만 다음과 같이 **근본적으로 다릅니다:**

| 구분 | 키로거 | HangulNotifier |
|------|--------|--------|
| **입력 저장** | 파일/DB/서버로 전송 | 저장 안 함, 로컬만 처리 |
| **후킹 방식** | 주입형(`WH_KEYBOARD`) 또는 드라이버 | 비주입형(`WH_KEYBOARD_LL`) |
| **코드 주입** | 다른 프로세스 메모리에 주입 | 주입 없음 |
| **권한** | 관리자 권한 요구 | 일반 사용자 권한 |
| **소스 공개** | 숨김 | 오픈소스 (검증 가능) |

---

## ⚠️ 백신 & SmartScreen 경고 안내

### 왜 경고가 나나요?

이 앱은 **코드 서명 인증서가 없습니다.** Windows Defender SmartScreen과 일부 백신은 서명 없는 프로그램, 특히 **전역 키보드 후킹을 쓰는 앱**을 의심하는 것이 정책입니다. 이는 정상적인 보안 설정입니다.

### 안전한 이유

1. **행위 기반 검증**
   - 입력 내용 저장/전송 없음(키로거의 1순위 신호 제거)
   - 네트워크 통신 코드 0
   - 프로세스 주입/메모리 조작 없음
   - 관리자 권한 미요구(`asInvoker`)

2. **투명성**
   - 오픈소스로 공개되어 소스 코드 검증 가능
   - 메타데이터, 아이콘, 버전 정보 완전 포함

### SmartScreen 경고 무시하기

첫 실행 시 SmartScreen이 경고하면:
1. 경고 대화상자에서 **"추가 정보"** 클릭
2. **"실행"** 버튼 클릭

이후 같은 버전은 경고하지 않습니다.

### 백신 제외 설정 (권장)

Windows Defender 또는 설치된 백신에서 `HangulNotifier.exe`를 제외(whitelist)하면 더 빠릅니다:
- **Windows Defender**: 설정 > 바이러스 및 위협 방지 > 관리 설정 > 제외 항목 추가

---

## 🎯 주요 기능

- **실시간 감지**: 타이핑 중 맞춤법 오류를 즉시 감지
- **캐럿 추적 알림**: 오류 위치 옆에 팝업으로 표시 (입력 중단 없음)
- **신뢰도별 알림**
  - 🔴 **Certain** (확실함): 띄어쓰기 오류, 확정 오타 등
  - 🟡 **Suspect** (의심): 문맥에 따라 틀릴 수 있는 표현
  - ⚪ **Info** (정보성): 참고만 필요한 항목
- **통계 대시보드**: 일별/주별/월별 감지 통계 조회
- **트레이 상주**: 시스템 트레이에서 일시정지/재개/설정 접근
- **사용자 규칙**: 개인 맞춤법 규칙 추가 가능
- **신뢰도별 제어**: 설정에서 각 수준의 알림 ON/OFF 선택

---

## 📝 감지 예시

실제 감지하는 주요 오류:

| 오류 입력 | 올바른 표기 | 설명 | 신뢰도 |
|----------|-----------|------|--------|
| `됬` | `됐` | '되었다'의 준말은 '됐다' | Certain |
| `되요` | `돼요` | '되어요'의 준말이라 '돼요'. ('하/해' 대입: '해요'가 자연스러우면 '돼요') | Certain |
| `되서` | `돼서` | '되어서'의 준말. ('해서'가 자연스러우면 '돼서') | Certain |
| `되야` | `돼야` | '되어야'의 준말. ('해야'가 자연스러우면 '돼야') | Certain |
| `돼고` | `되고` | 어미 앞 어간은 '되'. ('돼=되+어'라 어미가 붙으면 '되') | Certain |
| `않되` | `안 돼` | 부사 '안'은 띄어 씁니다. ('않'은 '-지 않다'에만) | Certain |
| `몇일` | `며칠` | 어떤 경우에도 '몇일'은 없음 | Certain |
| `오랫만` | `오랜만` | '오래간만'의 준말이라 '오랜만' | Certain |
| `금새` | `금세` | '금시에'의 준말이라 '금세' | Certain |
| `설레임` | `설렘` | 기본형이 '설레다'라 명사는 '설렘' | Certain |
| `희안` | `희한` | '희한(稀罕)하다'가 바른 표기 | Certain |
| `어의없` | `어이없` | '어이없다'가 바른 표기 | Certain |
| `되` (문장 끝) | `돼` | 문장을 끝맺을 땐 '돼' | Suspect |
| `안` (앞에 '지') | `않` | '-지 안'이 아니라 '-지 않' | Suspect |

### 되/돼 구분 원리

가장 흔한 오류는 '되'와 '돼' 혼동입니다. HangulNotifier는 다음 원리로 구분합니다:

```
돼 = 되 + 어

예:
- 되어요 → 돼요 ('해요'처럼 자연스러움)
- 되어서 → 돼서
- 되어야 → 돼야
- 되고 → 되고 (어미가 붙으므로 '되'는 그대로)

자동 검사법: '되' 자리에 '하'를, '돼' 자리에 '해'를 넣어보기
- 하고 vs 해고? → '하고'가 맞으므로 '되고'
- 해도 vs 하도? → '해도'가 맞으므로 '돼도'
```

---

## 🛠 기술 스택

| 계층 | 기술 |
|------|------|
| **런타임** | .NET 8 (`net8.0-windows`) |
| **UI 프레임워크** | WPF + DevExpress WPF |
| **MVVM** | DevExpress.Mvvm + CodeGenerators (`[GenerateViewModel]`, `[GenerateProperty]`) |
| **테마** | DevExpress.Wpf.ThemesLW (Win11Light) |
| **차트/그리드** | DevExpress.Wpf ChartControl, GridControl |
| **트레이** | H.NotifyIcon.Wpf |
| **DB** | Microsoft.Data.Sqlite (통계 저장소) |
| **DI/호스팅** | Microsoft.Extensions.Hosting |
| **로깅** | Serilog (롤링 파일 싱크) |
| **설정** | System.Text.Json |
| **테스트** | xUnit + FluentAssertions |

### 아키텍처 특징

- **Core 계층 순수성**: 맞춤법 엔진(`HangulAutomata`, `WordBuffer`, `RuleEngine`)은 .NET 8 표준만 사용 → 100% 테스트 가능, 플랫폼 의존성 0
- **Platform 계층 격리**: 모든 P/Invoke와 Windows API를 별도 프로젝트로 분리
- **비차단 후킹**: 키보드 후킹 콜백은 10ms 이내 반환, 실제 처리는 워커 스레드에서 비동기 수행

---

## 🏗 빌드 & 설치

### 요구사항

- **Windows 10 또는 11** (64-bit)
- **.NET 8 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **.NET 8 Desktop Runtime** (설치 시 필요)

### 빌드

```bash
# 솔루션 빌드
dotnet build

# 테스트 실행
dotnet test

# Release 빌드
dotnet build -c Release

# 단일 파일 게시 (Windows x64)
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=false \
  -p:SelfContained=false
```

게시된 실행 파일은 `src/HangulNotifier.App/bin/Release/win-x64/publish/HangulNotifier.exe`

---

## 개발 현황

**상태:** Phase 2 진행 중

- ✅ Phase 0: 솔루션 구조, 패키지 설정 완료
- ✅ Phase 1: Core(자모/오토마타/버퍼/규칙 엔진) 완료
- 🔄 Phase 2: Platform(키보드 후킹/IME 상태/보안 필드 감지) **진행 중**
- ⏳ Phase 3: 오버레이 UI (캐럿 추적, 알림 렌더링)
- ⏳ Phase 4: 통합 (트레이, 설정, 통계 대시보드)
- ⏳ Phase 5: 마감 (AV 하드닝, InnoSetup 인스톨러, 문서화)

---

## 📌 알려진 제약

1. **관리자 권한 앱 미지원**
   - 작업 관리자, 레지스트리 편집기, 관리자 모드 명령 프롬프트 등 관리자 권한으로 실행되는 앱에서는 동작하지 않습니다.
   - Windows 보안 정책에 의한 제약입니다.

2. **안티치트 게임 제외**
   - 일부 게임(발로란트 등 Riot Vanguard 탑재)은 전역 후킹을 차단하거나 앱을 강제 종료할 수 있습니다.
   - 게임 프로세스는 기본적으로 감지 대상에서 제외됩니다.

3. **백신 오탐 (서명 없음)**
   - 코드 서명 인증서가 없어 첫 실행 시 SmartScreen 경고가 나타날 수 있습니다.
   - "추가 정보 → 실행"으로 무시 가능하며, 오픈소스 공개로 검증 가능합니다.

4. **일부 UWP/Store 앱 캐럿 미감지**
   - Microsoft Store 앱 중 일부는 표준 API로 캐럿 위치를 가져올 수 없습니다.
   - 이 경우 고정 위치에 알림을 표시하는 폴백 모드로 동작합니다.

---

## 📄 라이선스

개인 프로젝트. 라이선스는 추후 명시 예정입니다.

---

**Made with ❤️ by H.Soft, Jeju**

HangulNotifier는 한글 사용자의 쾌적한 타이핑 경험을 위해 만들어졌습니다.
