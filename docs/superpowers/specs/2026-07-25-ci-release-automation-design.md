# CI/CD 릴리즈 자동화 설계

- 날짜: 2026-07-25
- 대상: `BaeTab/hangeul_detect` (HangulNotifier)
- 산출물: `.github/workflows/release.yml`

## 목표

버전 태그를 push하면 빌드·테스트·인스톨러 컴파일·체크섬·GitHub 릴리즈 발행이 **자동**으로 이뤄지게 한다. 그동안 수동으로 하던 publish → ISCC → release 과정을 파이프라인화한다.

## 제약 (프로젝트 하드 요구)

- **무서명 배포(AV 하드닝):** 코드 서명·비밀값 없음. 클라우드 빌드라도 서명 단계 없음.
- **최소 의존성:** 서드파티 GitHub Action은 GitHub 1st-party(`actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`)만 사용. 릴리즈 발행은 러너에 내장된 `gh` CLI + `GITHUB_TOKEN` 으로 처리(외부 릴리즈 액션 미사용).
- **Inno Setup 7:** 로컬(개발 PC)엔 `D:\Program Files\Inno Setup 7`(v7.0.2)만 있으나, 러너에는 없음 → 워크플로가 공식 설치본을 내려받아 무인 설치한다.

## 결정

### 러너
GitHub 호스티드 `windows-latest`. 공개 리포라 무료, 개발 PC 상태와 무관하게 동작, 무서명이라 비밀값 불필요.

### 버전 모델 — "파일이 진실, 태그가 검증"
- 진실의 원천: `Directory.Build.props`의 `VersionPrefix` (그리고 이와 일치해야 하는 `HangulNotifier_installer.iss`의 `#define MyAppVersion`).
- 릴리즈 절차: 두 파일의 버전을 올려 커밋 → `git tag vX.Y.Z && push`.
- 워크플로는 **태그 == Directory.Build.props == .iss** 를 검증하고, 불일치면 실패시킨다. → 리포가 항상 정확한 버전을 표시(AV 메타데이터 정직), 기존 수동 관습과 일치.
- 프리릴리즈: `-`가 붙은 태그(`v0.3.0-beta`)는 `--prerelease`로 발행.

### 인스톨러(Inno Setup 7) 설치 방법
- 다운로드 URL(안정): `https://github.com/jrsoftware/issrc/releases/download/is-7_0_2/innosetup-7.0.2-x64.exe` (jrsoftware가 릴리즈를 GitHub에 호스팅). 검증됨(206, 17MB).
- `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=C:\InnoSetup7` 로 무인 설치 → `C:\InnoSetup7\ISCC.exe` 사용(경로 결정적).

### 릴리즈 노트
`gh api .../releases/generate-notes`로 자동 생성한 변경사항 앞에, 설치·SmartScreen·체크섬 검증·개인정보 안내 헤더를 붙여 발행.

## 워크플로 단계 (`windows-latest`)

1. `actions/checkout@v4` (fetch-depth: 0 — 노트 생성용 히스토리)
2. `actions/setup-dotnet@v4` (8.0.x)
3. **버전 검증** — 태그↔`Directory.Build.props`↔`.iss` 일치. 불일치 시 실패. `version`/`prerelease` output.
4. `dotnet test -c Release` — 실패 시 릴리즈 중단(Core+Rules).
5. `dotnet publish -c Release -p:PublishProfile=win-x64` — 프레임워크 종속 단일 파일(무압축).
6. **Inno Setup 7.0.2 설치** → `ISCC HangulNotifier_installer.iss` → `dist/HangulNotifier-Setup-<ver>.exe`.
7. **SHA256** 계산 → `dist/HangulNotifier-Setup-<ver>.exe.sha256`.
8. `actions/upload-artifact@v4` — 인스톨러+체크섬(항상, 드라이런 포함).
9. **릴리즈 발행** — 태그 push 에서만(`if: startsWith(github.ref, 'refs/tags/')`). `gh release create` 로 인스톨러+체크섬 첨부, 노트 발행.

## 트리거

- `push` tags: `v[0-9]+.[0-9]+.[0-9]+`, `v[0-9]+.[0-9]+.[0-9]+-*`
- `workflow_dispatch`: 발행 없이 빌드/테스트/아티팩트만(드라이런).

## 실패 안전

- 테스트 실패 · 버전 불일치 · 빌드/컴파일 실패 → 잡 실패, 릴리즈 미발행. 부분 산출물은 릴리즈에 올라가지 않음(아티팩트만 존재).
- `if-no-files-found: error` — 산출물 없으면 실패.

## 업데이트 확인기와의 관계

앱의 옵트인 업데이트 확인기(`UpdateChecker`)는 릴리즈의 **버전 문자열**만 조회한다. CI가 체크섬(`.sha256`)을 릴리즈에 게시함으로써 무서명 배포의 신뢰를 보강(사용자 수동 검증용)한다. 앱은 다운로드/설치를 하지 않으므로 **앱 코드 변경은 없다.**

## 검증 (로컬 선행)

워크플로가 실행할 명령을 개발 PC의 Inno Setup 7으로 사전 검증 완료:
`dotnet test`(139 통과) → `dotnet publish` → **v7 `ISCC HangulNotifier_installer.iss`**(정상 컴파일, `HangulNotifier-Setup-0.2.1.exe` 생성). `.iss`가 v7과 호환됨을 확인.

## 범위 밖(YAGNI)

- 코드 서명(하드 요구 "무서명"과 상충 → 별도 결정 사안).
- 앱 내 자동 다운로드/설치(옵트인 알림형 철학 유지).
- winget/기타 배포 채널.
