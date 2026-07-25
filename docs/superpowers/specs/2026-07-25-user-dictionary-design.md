# 사용자 사전 / 화이트리스트 설계

- 날짜: 2026-07-25
- 대상: `BaeTab/hangeul_detect` (HangulNotifier)
- 목적: 오탐(false positive) 차단 — 사용자가 "맞다"고 인정한 어절은 알림/통계에서 제외한다.

## 배경

이 앱의 최우선 가치는 **오탐 제로**다. 고유명사·전문용어·의도적 표현이 규칙에 걸리는 경우, 사용자가 해당 어절을 예외로 등록해 알림을 끌 수 있어야 한다. 기존 억제 수단(규칙 개별 OFF, 프로세스 제외)과 달리 **단어 단위** 예외다.

## 제약

- **프라이버시:** 100% 로컬. 사용자가 명시적으로 등록한 단어만 저장(입력 텍스트 자동 저장 아님). 네트워크 전송 없음.
- **동시 작업 안전:** 규칙 확장 세션이 `Core/Rules`를 대대적으로 수정 중 → **Core를 건드리지 않고** App 계층에서만 구현한다.

## 결정

### 필터 위치 — App 계층(`DetectionPipeline`)
`RuleEngine.Check()`는 그대로 두고, `DetectionPipeline.OnCheckRequested`에서 감지 결과가 나온 뒤 화이트리스트를 적용한다. `wc.Word`(전체 어절)가 이미 그 자리에 있으므로 Core 변경이 필요 없다. → 동시 세션과 충돌 zero.

```
_engine.Check(word, prev) → detections
if (detections.Count == 0) return;
if (IsWhitelisted(word)) return;   // ← 추가: 알림·통계·사운드 모두 건너뜀
...
```

### 매칭 방식 — 정확히 일치(exact)
입력 어절이 등록 단어와 `StringComparison.Ordinal`로 **정확히 같을 때만** 제외. 예측 가능하고 **과억제(진짜 오류 누락) 위험이 없다.** 조사가 붙은 형태(`돼지런에`)는 별도 등록이 필요하다(맞춤법 도구라 안전을 우선).

### 저장 — `AppSettings.WhitelistWords: List<string>`
`settings.json`에 저장. `ExcludedProcesses`와 동일 패턴. 단, 프로세스명과 달리 **소문자 변환하지 않고** 원문 그대로 저장(한글).

### 실시간 반영
`_settings`는 참조로 공유되어 설정 저장 즉시 `DetectionPipeline`이 최신 목록을 읽는다. **재시작 불필요.**

### UI
설정 창에 "사용자 사전(예외 단어)" 카드 — 여러 줄 텍스트박스(`WhitelistBox`), 한 줄에 한 단어. `감지 제외 프로세스` 카드 바로 아래 배치(개념적으로 인접).

## 구성 요소

| 파일 | 변경 |
|------|------|
| `Configuration/AppSettings.cs` | `WhitelistWords` 추가 |
| `Services/DetectionPipeline.cs` | `IsWhitelisted(word)` + `OnCheckRequested`에서 조기 반환 |
| `Views/SettingsWindow.xaml` | "사용자 사전" 카드 + `WhitelistBox` |
| `Views/SettingsWindow.xaml.cs` | load/save 배선(원문 보존) |
| `README.md` | 기능 한 줄 |

## 범위 밖(YAGNI / v2 후보)

- 트레이 "마지막 감지 단어를 사전에 추가" 빠른 등록(발견성↑) — App/트레이 결합 증가라 v2로.
- 접두/부분 일치, 조사 자동 처리 — 과억제 위험이 있어 보류.
- Core `RuleEngineOptions`로의 통합 — 동시 세션 안정화 이후 검토.

## 검증

- 빌드(오류 0), 설정 카드 렌더 확인.
- 로직: 등록 어절과 정확히 일치할 때만 제외(대소문자 구분), 목록이 비면 무시 없음.
