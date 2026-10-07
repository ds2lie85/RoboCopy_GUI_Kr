# 개발 도구 및 배포 절차

확인일: 2026-10-07  
제품 버전: 1.0

## 필수 환경

| 도구 | 용도 |
|---|---|
| Windows 10 버전 1607(x64, 빌드 14393) 이상 | WPF 및 Windows Shell/Restart Manager 실행 |
| Windows PowerShell 5.1 | 빌드·게시·설치 스크립트 |
| Windows `robocopy.exe` | 복사 엔진 |
| .NET SDK 8.0.425 x64 | 빌드·테스트·게시 (`global.json`) |
| Git for Windows | GitHub/GitLab 버전 관리 및 업로드 |

Visual Studio 2022나 VS Code는 선택 사항이다. Visual Studio를 쓰면 `.NET 데스크톱 개발` 워크로드를 선택한다.

## 새 개발 컴퓨터 준비

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Setup-LocalSdk.ps1
.\Build.ps1
```

`Setup-LocalSdk.ps1`은 SDK를 저장소의 `.dotnet`에 준비한다. `.dotnet`, `.nuget`과 CLI 홈은 로컬 개발용이며 `.gitignore`로 원격 저장소에서 제외된다.

## 일상 개발 검증

```powershell
.\Build.ps1
```

개별 명령이 필요하면 다음과 같이 실행한다.

```powershell
.\.dotnet\dotnet.exe restore RoboCopy_GUI_Kr.sln --configfile NuGet.Config -p:NuGetAudit=false -m:1
.\.dotnet\dotnet.exe build RoboCopy_GUI_Kr.sln -c Release --no-restore -m:1
.\.dotnet\dotnet.exe test RoboCopy_GUI_Kr.sln -c Release --no-build -m:1
```

인터넷 연결 환경의 패키지 보안 감사:

```powershell
.\.dotnet\dotnet.exe restore RoboCopy_GUI_Kr.sln --configfile NuGet.Config -p:NuGetAudit=true -m:1
```

## 개발 빌드 실행

```powershell
.\Run-Development.ps1
```

개발 빌드는 `artifacts\development\RoboCopy_GUI_Kr_Development`에 생성되며 앱, 테스트 DLL, 잠금 시험 도우미와 소스 스냅샷을 보존한다.

## 1.0 설치기 생성

```powershell
.\Build-Installer.ps1
```

결과 파일은 `artifacts\installer\RoboCopy_GUI_Kr_Setup_v1.0_x64.exe`이다. IExpress 기반 단일 설치 파일 안에 다음 두 페이로드가 들어간다.

설치기는 Windows 10 버전 1607(x64, 빌드 14393)을 최소 조건으로 검사한다. Windows 7/8.1은 현재 .NET 8 배포 대상에서 제외하며, 필요 시 별도 레거시판으로 개발한다.

- .NET 8 Desktop Runtime이 있는 PC용 framework-dependent 빌드
- 인터넷이나 런타임이 없는 PC용 self-contained 빌드

설치 대상은 현재 사용자 `%LOCALAPPDATA%\Programs\RoboCopy_GUI_Kr`이며, 시작 메뉴·바탕 화면 바로가기와 탐색기 연동을 설치 화면에서 선택한다. 제거 시 사용자 작업 설정과 로그는 유지한다.

## 공개 저장소 준비 규칙

- 커밋: 소스, 테스트, `test-assets`, 설치 스크립트, 문서, 솔루션/프로젝트 파일
- 커밋 제외: `bin`, `obj`, `TestResults`, `.vs`, 로컬 SDK/NuGet/CLI 캐시, 로그, `artifacts`
- 설치 EXE는 Git 저장소에 직접 넣기보다 GitHub/GitLab Release 자산으로 게시한다.
- API 키, 토큰, 개인 경로가 새 파일에 포함되지 않았는지 업로드 전에 다시 검색한다.

현재 작업 폴더의 개발·테스트·배포 산출물은 로컬 검증을 위해 보존하되 `.gitignore`로 원격 추적에서 제외한다.
