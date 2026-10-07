# RoboCopy_GUI_Kr 개발 현황

- 제품 버전: **1.0** (`1.0.0.0` 파일/어셈블리 버전)
- 최종 갱신: 2026-10-07
- 대상 환경: Windows 10 버전 1607(x64, 빌드 14393) 이상, .NET 8 WPF
- 기준 문서: `AGENTS.md`, `Robocopy_GUI_개발계획서.md`

## 1.0 완료 범위

- 복사, 안전 이동, 분석/미리보기, 단방향 동기화(백업/지속)
- 재시작 가능한 `/Z`, 재시도, 빈 폴더·0Byte·심볼릭 링크·예외 규칙
- 자동/수동 멀티스레드와 실제 원본 표본 벤치마크 및 30일 성능 기록
- 작업 프로필, 순차 작업 큐, 실행 상태 복구, 로그 및 결과 요약
- 탐색기 우클릭 복사/이동/백업/지속 동기화와 기본값 즉시 시작
- 메인 UI와 Windows 복사창 형태의 미니 UI, 바이트 기준 전체 진행률
- 지속 동기화 전용 트레이 에이전트, 충돌 작업 선택·변경·삭제
- 지속 동기화 완료 알림, 7일 알림 이력, 상세 복사·삭제 목록
- 자동 삭제 항목 7일 격리 및 복원
- 오류 파일 관리, 일반/관리자 재시도, Restart Manager 잠금 조회
- 정상 종료 실패 후에만 노출되는 명시적 경고·승인 기반 강제 종료
- 긴 경로의 창 너비 대응 중간 생략과 전체 경로 툴팁
- 사용자 표시 문자열의 리소스 분리와 `ArgumentList` 기반 인자 전달

## 주요 프로젝트

| 경로 | 역할 |
|---|---|
| `src/RoboCopy_GUI_Kr` | 메인 WPF UI, 미니 UI, 탐색기 연동 |
| `src/RoboCopy_GUI_Kr.Core` | 명령 생성, 실행, 검증, 프로필, 지속 동기화 모델 |
| `src/RoboCopy_GUI_Kr.Agent` | 트레이 상주 지속 동기화 에이전트 |
| `src/RoboCopy_GUI_Kr.ElevatedWorker` | 선택 오류 항목의 관리자 권한 재시도 |
| `src/RoboCopy_GUI_Kr.LockTestHelper` | 잠금·정상 종료·강제 종료 수동 시험 도우미 |
| `tests/RoboCopy_GUI_Kr.Core.Tests` | 단위·통합·UI 계약 테스트 |
| `test-assets` | 향후 회귀/수동 시험용 자료 |

## 빌드와 검증

```powershell
.\Build.ps1
```

이 스크립트는 복원, Release 빌드, 전체 테스트, self-contained 개발 빌드 생성과 개발용 탐색기 메뉴 등록을 수행한다.

- 개발 빌드: `artifacts\development\RoboCopy_GUI_Kr_Development`
- 앱: `artifacts\development\RoboCopy_GUI_Kr_Development\app\RoboCopy_GUI_Kr.exe`
- 테스트 도우미: `artifacts\development\RoboCopy_GUI_Kr_Development\test-tools`
- 소스 스냅샷: `artifacts\development\RoboCopy_GUI_Kr_Development\source`

배포 설치기는 다음 명령으로 만든다.

```powershell
.\Build-Installer.ps1
```

- 결과: `artifacts\installer\RoboCopy_GUI_Kr_Setup_v1.0_x64.exe`
- 온라인용 framework-dependent 앱과 오프라인용 self-contained 앱을 함께 포함한다.
- 설치 중 Windows/Robocopy/.NET Desktop Runtime을 검사한다.
- Windows 클라이언트 최소 조건은 Windows 10 버전 1607(x64, 빌드 14393)이다.
- 설치 중간 staging 폴더는 성공 후 자동 제거한다.

## 안전성 불변 조건

- Robocopy 인자는 `ProcessStartInfo.ArgumentList`에 토큰 단위로 전달한다.
- Robocopy 종료 코드는 비트 플래그로 판정한다.
- 이동은 복사와 바이트 검증에 성공한 원본만 삭제한다.
- 권한 부족은 ACL을 바꾸지 않고 Elevated Worker로 선택 재시도한다.
- 잠금 프로세스 강제 종료는 정상 종료 실패 후 사용자 명시 승인 때만 가능하다.
- 양방향 충돌은 자동 덮어쓰지 않는다.
- 삭제 작업은 사용자 확인과 대상 경로 안전성 검사를 유지한다.

## 1.0 이후 후보

현재 필수 개발 항목은 남아 있지 않다. 이후 작업은 실사용 회귀 시험 결과에 따라 결함 수정, 코드 서명, CI 빌드, GitHub/GitLab 릴리스 자동화 순으로 진행한다.
