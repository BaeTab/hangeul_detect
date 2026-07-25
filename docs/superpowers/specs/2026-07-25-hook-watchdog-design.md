# 후킹 자동 복구(워치독) 설계

- 날짜: 2026-07-25
- 대상: `BaeTab/hangeul_detect` (HangulNotifier)
- 목적: 상주 앱이 "살아있는데 감지만 죽어있는" 상태를 스스로 복구한다. (v1.0 블로커)

## 문제

Windows는 저수준 후킹(`WH_KEYBOARD_LL`)의 콜백이 `LowLevelHooksTimeout`(기본 약 5초, HKCU\Control Panel\Desktop)을 초과하면 **해당 후킹을 조용히 제거**한다. 이때:

- `UnhookWindowsHookEx`가 호출되지 않으므로 `_hookHandle`은 여전히 non-zero.
- 후킹 스레드와 메시지 루프도 그대로 살아있어 `IsRunning == true`.
- **콜백만 더 이상 오지 않는다.**

즉 트레이 아이콘도 정상, 설정·통계도 정상인데 **맞춤법 감지만 영구히 멈춘다.** 사용자는 앱을 재시작하기 전까지 이 사실을 알 수 없다. 이 앱은 백그라운드 상주가 전제라 1.0 요건으로 자동 복구가 필요하다.

발생 조건: 시스템 과부하, 절전/최대 절전 복귀, 원격 세션 전환 등에서 콜백이 지연될 때.

## 결정 — 시스템 입력 시각과 콜백 시각 대조

Windows는 후킹 제거를 통보하지 않으므로 **간접 증거**로 판단한다.

- `GetLastInputInfo()`는 후킹과 무관하게 **시스템 전역의 마지막 입력 시각**을 알려준다(입력 내용은 없음, 시각만).
- `KeyboardHook.LastCallbackTicks`는 우리 콜백이 마지막으로 들어온 시각.
- **시스템은 입력을 받았는데 우리 콜백은 그보다 한참(10초) 전에 멈춰 있으면** 후킹이 제거된 것으로 본다.

### 오진 처리 (마우스 전용 구간)

`GetLastInputInfo`는 **마우스 입력도 포함**한다. 따라서 "마우스만 쓰는 구간"은 후킹이 멀쩡해도 죽은 것처럼 보인다. 이를 다음으로 억제한다.

- **지수 백오프:** 재설치 후에도 콜백이 없으면 진단이 틀렸을 가능성이 크므로 간격을 늘린다 — 1분 → 5분 → 15분 → 30분(상한).
- **백오프 초기화:** 재설치 이후 **실제 콜백**이 들어오면(= 진단이 맞았음) 인덱스를 0으로 되돌린다.
  - 주의: `Reinstall()` 직후 `LastCallbackTicks`는 설치 시각으로 갱신되므로, 기준선(`_reinstallBaselineCallback`)은 **재설치 완료 후**의 값으로 잡아야 한다. 그렇지 않으면 백오프가 즉시 초기화된다.

결과적으로 마우스 전용 사용자는 최악의 경우 30분에 1회의 무해한 예방적 재설치만 발생한다.

### 안전장치

- **타이핑 중 미개입:** `IdleMs() >= 2초`일 때만 재설치한다. 조합 중인 입력을 건드리지 않는다.
- **일시정지 존중:** `IsPaused`면 아무것도 하지 않는다(후킹 해제가 의도된 상태).
- **점검 주기 30초:** 기존 워커 루프(100ms 틱)에 얹어 별도 타이머·스레드를 만들지 않는다.

## 부수 개선 — 설치 실패 시 프로세스 종료 방지

기존 `HookThreadProc`은 `SetWindowsHookEx` 실패 시 **백그라운드 스레드에서 예외를 던졌다.** .NET에서 이는 프로세스 종료로 이어진다(전역 핸들러는 로깅만 하고 막지 못함). 복구 경로에서 앱이 죽으면 안 되므로 예외 대신 `InstallFailed(int win32Error)` 이벤트로 알리고, 워치독이 나중에 재시도한다.

## 구성 요소

| 파일 | 변경 |
|------|------|
| `Platform/Native/NativeMethods.cs` | `LASTINPUTINFO`, `GetLastInputInfo` 추가 |
| `Platform/Input/SystemInputInfo.cs` (신규) | `IdleMs()`, `LastInputTicks64()`. `GetTickCount` 32비트 순환을 부호 없는 뺄셈으로 안전 처리 |
| `Platform/Hooking/KeyboardHook.cs` | `LastCallbackTicks`(콜백에서 `Volatile.Write` — 지연 없음), `Reinstall()`, `InstallFailed` 이벤트, 설치 시 기준 시각 초기화 |
| `App/Services/DetectionPipeline.cs` | `HookWatchdog()` + `TryReinstall()`, 워커 루프에 연결, `--diag`에 `reinstalls=` 카운터 |

## 검증

- 빌드 오류 0, 테스트 **260개 전부 통과**.
- `--diag` 구동 후 **불필요한 재설치가 발생하지 않는지**(reinstalls=0 유지) 로그로 확인.
- **한계:** 실제 후킹 강제 제거는 재현이 어렵다(시스템 부하 유발 필요). 워치독의 판정 로직은 시각 비교라 결정적이며, 오작동 방향(과도한 재설치)은 백오프로 억제된다. 장시간 실사용에서 `reinstalls` 카운터로 계속 관찰한다.

## 범위 밖

- 후킹 콜백 자체의 지연 측정/경고(현재 구조상 콜백은 Channel 쓰기만 하므로 지연 위험이 낮다).
- 마우스 후킹을 추가해 오진을 없애는 방안 — 침습적이고 AV 하드닝에 불리해 채택하지 않음.
