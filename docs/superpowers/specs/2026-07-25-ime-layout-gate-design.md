# IME 감지 개선 — 키보드 레이아웃 게이트 설계

- 날짜: 2026-07-25
- 대상: `BaeTab/hangeul_detect` (HangulNotifier)
- 목적: TSF 앱 미확정 구간의 한/영 desync 오탐을 줄인다.

## 배경 (현재 구현)

`ImeStateReader.TryQueryDefinitive`는 포커스 컨트롤의 기본 IME 창에 `WM_IME_CONTROL`(IMC_GETCONVERSIONMODE/OPENSTATUS)을 `SendMessageTimeout(ABORTIFHUNG, 80ms)`로 질의한다.
- **클래식(Win32) 앱:** 확답 → 정확 판정.
- **최신 TSF 앱(크롬/카톡/UWP/VS):** 응답 없음 → 미확정. 이때 파이프라인은 전역 후킹이 본 한/영 토글키(VK_HANGUL)로 `_assumedHangul`을 로컬 추적한다.
- `AttachThreadInput`은 IME 조합을 방해하므로 **금지(앱 절대 원칙)**.

## 문제

미확정(TSF) 구간에서 `Win+Space`나 언어바로 언어를 바꾸면 VK_HANGUL 이벤트가 없어 `_assumedHangul`이 어긋난다(desync). 그 결과 영문 입력 중인데 한글 맞춤법 오탐이 뜰 수 있다.

## 결정 — 키보드 레이아웃 게이트 (교차 프로세스 읽기 가능 신호 활용)

`GetKeyboardLayout(foregroundThreadId)`의 하위 LANGID는 **입력 언어(HKL)**를 교차 프로세스에서도 알려준다. 한국어 키보드면 `0x0412`(primary `0x12`), 영문(US) 키보드면 `0x0409`. 이는 이미 `ImeStateReader.Query`가 참고용으로 읽고 있으나 판정엔 쓰지 않던 값이다.

- **게이트:** 미확정 구간에서 `_assumedHangul == true` 라도, 포그라운드 레이아웃이 **확실히 비한국어**면 감지하지 않는다.
- **fail-open:** langId 를 못 읽으면(0) 게이트를 적용하지 않고 기존대로 통과 → **감지 누락 회귀 위험 없음**.
- **한계:** 한국어 IME 하나만 두고 Win+Space가 그 IME의 한/영만 토글하는 구성에서는 langId 가 계속 `0x0412`라 이 게이트로는 못 잡는다(이 경우는 여전히 VK_HANGUL 추적 + 수동 재동기화에 의존). 다중 키보드(한국어+영문) 전환 desync를 해소한다.

## 구현

| 파일 | 변경 |
|------|------|
| `Platform/Ime/ImeStateReader.cs` | `ForegroundLayoutIsNonKorean()` 추가 (GetKeyboardLayout, 조회 전용, fail-open) |
| `App/Services/DetectionPipeline.cs` | `ForegroundOkAndHangul`의 미확정 분기에 게이트 적용. 차단 시 `--diag` `lastGate="layout"` |
| `App/Services/DetectionPipeline.cs` | `[DIAG]` 로그에 `lang=0x..` 추가(관측성) |
| `README.md` | 알려진 제약 #1 문구 보강 |

```
if (_ime.TryQueryDefinitive(out imm))        → 확답 사용
else if (_assumedHangul && _ime.ForegroundLayoutIsNonKorean())  → layout 게이트로 차단
else                                          → _assumedHangul
```

## 검증

- 빌드 오류 0.
- **한계:** IME/언어전환은 실제 앱에서만 재현되어 완전 자동검증 불가. `--diag` 실행 후 크롬/메모장 등에서 Win+Space로 영문 전환 → `[DIAG] ... lastGate=layout ... lang=0x409` 로그로 게이트 동작을 확인한다.

## 범위 밖(사용자가 이번에 미선택)

- Win+Space 후크 감지(버퍼 리셋/재질의).
- 트레이 수동 한/영 토글.
