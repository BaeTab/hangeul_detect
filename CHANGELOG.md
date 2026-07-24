# Changelog

한글 맞춤법 실시간 알림기 **HangulNotifier**의 모든 변경 사항을 기록합니다.

버전 관리는 [Semantic Versioning](https://semver.org/lang/ko/) 을 따릅니다.

---

## [0.1.0] - 2026-07-24

### Added

- **Core 맞춤법 엔진**
  - 두벌식 한글 오토마타: 종성 이월, 겹받침 처리 완전 구현
  - 어절 단위 입력 버퍼: 실시간 조합 중 단어 경계 자동 감지
  - 규칙 엔진: 되/돼, 띄어쓰기, 어미 등 1,000+ 규칙 지원
  - 신뢰도 분류: Certain (확실), Suspect (의심), Info (정보성)
  - 단위 테스트 97개 작성, 오토마타 커버리지 100%

- **Platform 계층 (Windows API)**
  - 전역 키보드 후킹 (`WH_KEYBOARD_LL`): 비주입형, 관리자 권한 불필요
  - IME 상태 판정: 한글/영문 모드 자동 감지
  - 비밀번호 필드 3중 차단
    - `<input type="password">` 웹 폼 감지
    - Windows 보안 필드 감지
    - 기타 숨김 필드 감지
  - 캐럿(텍스트 커서) 위치 추적
  - 멀티 모니터 좌표 변환

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
  - 통계 대시보드 (DevExpress Chart/Grid)
    - 일별/주별/월별 감지 통계
    - 규칙별 감지 빈도 분석
    - 상위 프로세스 랭킹
  - SQLite 통계 저장소 (`stats.db`)
    - 입력 텍스트 미저장 (규칙ID, 타임스탬프, 프로세스명만 기록)
  - Serilog 롤링 로그: 일별 아카이브, 최근 30일 유지

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
