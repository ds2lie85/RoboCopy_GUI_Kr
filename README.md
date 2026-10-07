# RoboCopy_GUI_Kr 1.0

A Windows desktop application built with .NET 8 and WPF that provides a Korean interface for Windows `robocopy.exe`. It supports copying, verified moves, and one-way backup or continuous synchronization. Maintained by **ds2lie85**.

Windows `robocopy.exe`를 한국어 UI로 사용할 수 있는 .NET 8 WPF 데스크톱 애플리케이션입니다. 복사, 검증 후 이동, 단방향 백업 및 지속 동기화를 지원합니다. 저작권자 및 관리자는 **ds2lie85**입니다.

## Installation / 설치

Download `RoboCopy_GUI_Kr_Setup_v1.0_x64.exe` from the repository's **Releases** page on GitHub or GitLab and run it. The installer checks Windows, Robocopy, connectivity, and .NET 8 Desktop Runtime. With an internet connection, it can offer Microsoft's official runtime installer. For offline use or a failed runtime installation, it uses the included self-contained application. Select shortcuts, Explorer integration, and whether to launch the app after installation.

GitHub 또는 GitLab 저장소의 **Releases** 페이지에서 `RoboCopy_GUI_Kr_Setup_v1.0_x64.exe`를 내려받아 실행하세요. 설치기는 Windows, Robocopy, 인터넷 연결 및 .NET 8 Desktop Runtime을 확인합니다. 인터넷에 연결되어 있으면 Microsoft 공식 런타임 설치를 선택할 수 있습니다. 오프라인이거나 런타임 설치에 실패하면 포함된 자체 포함 앱을 사용합니다. 바로가기, 탐색기 연동 및 설치 후 실행 여부를 선택할 수 있습니다.

The declared minimum operating system is **Windows 10 version 1607, x64 (build 14393)**. Windows 11 x64 is also supported. A separate .NET SDK is not required for the installed application. Windows 7/8.1 and native ARM64 builds are not provided. The installer has been successfully installed and run on another computer by the maintainer; this does not represent testing on every supported Windows release.

프로젝트의 최소 운영체제 기준은 **Windows 10 버전 1607, x64(빌드 14393)**이며 Windows 11 x64도 지원합니다. 설치된 앱을 실행할 때 별도의 .NET SDK는 필요하지 않습니다. Windows 7/8.1 및 ARM64 네이티브 빌드는 제공하지 않습니다. 관리자가 다른 컴퓨터에서 설치와 실행을 확인했으며, 모든 지원 Windows 버전을 시험했다는 의미는 아닙니다.

## Quick start / 빠른 시작

Choose the source and destination folders, select a copy or synchronization mode, and use analysis/preview before starting. Adjust exclusions, thread count, and retry settings as needed. Review deletion choices for synchronization and keep a separate backup before using move or deletion operations on important data.

원본과 대상 폴더를 선택하고 복사 또는 동기화 방식을 지정한 뒤 분석/미리보기로 작업을 확인하세요. 필요하면 예외, 스레드 수, 재시도 설정을 조정하세요. 동기화의 삭제 항목을 검토하고, 중요한 데이터를 이동하거나 삭제하기 전에는 별도의 백업을 보관하세요.

## Features / 주요 기능

### Copying and progress / 복사 및 진행률

Copy files with restartable mode (`/Z`), preview (`/L`), retries (`/R`, `/W`), empty-folder options (`/S`, `/E`), zero-byte exclusions, file/folder exclusions (`/XF`, `/XD`), and link/junction policies. A lazily loaded exclusion tree supports parent/child selections; pattern lists support multiple selection with Ctrl/Shift and dragging.

재시작 가능 모드(`/Z`), 미리보기(`/L`), 재시도(`/R`, `/W`), 빈 폴더 옵션(`/S`, `/E`), 0Byte 제외, 파일·폴더 예외(`/XF`, `/XD`) 및 링크·정션 정책으로 복사합니다. 예외 폴더 트리는 지연 로딩과 상위·하위 선택 연동을 지원하며, 패턴 목록은 Ctrl/Shift와 드래그 복수 선택을 지원합니다.

Switch between the main window and a compact copy window. Workload analysis provides overall progress; destination write monitoring shows files being copied in parallel. Completed files leave the list automatically. Resizable log windows, result summaries, adaptive path shortening, and full-path tooltips help inspect long paths and large jobs. High-volume output is buffered and delivered to the UI in batches.

메인 창과 작은 복사창을 전환할 수 있습니다. 작업량 분석으로 전체 진행률을 계산하고 대상 쓰기를 감시하여 병렬 복사 파일을 표시합니다. 완료된 파일은 목록에서 자동으로 제거합니다. 크기 조절 로그 창, 결과 요약, 창 너비에 맞춘 경로 생략 및 전체 경로 툴팁으로 긴 경로와 대량 작업을 확인할 수 있습니다. 대량 출력은 버퍼에 모아 UI에 전달합니다.

### Thread recommendations / 스레드 수 추천

Choose `/MT:1–128` manually or use automatic recommendations. Reassessment benchmarks readable source samples, records CPU/RAM/storage details where available, and caches volume-pair results for 30 days. The main copy mode selects the lowest thread count reaching 95% of the best measured speed. History supports review and confirmed deletion; actual copy measurements carry more weight than sample benchmarks.

`/MT:1–128`을 직접 지정하거나 자동 추천을 사용할 수 있습니다. 재검토는 읽을 수 있는 원본 표본으로 성능을 측정하고 조회 가능한 CPU·RAM·저장장치 정보를 기록하며 볼륨 조합별 결과를 30일간 보관합니다. 일반 복사는 최고 측정 속도의 95% 이상인 후보 중 가장 낮은 스레드 수를 추천합니다. 이력 조회와 확인 후 삭제를 지원하며 실제 복사 결과에는 표본 측정보다 높은 가중치를 적용합니다.

### Verified moves / 검증 후 이동

Move mode first copies files and compares source and destination contents byte by byte. Only successfully verified source files are deleted. On errors, choose whether to move verified items first, wait until all errors are resolved, stop, or clean up new destination files from this run. Cleanup requires confirmation and preserves files that existed before the run. Paused move/cleanup phases resume their remaining items; stopping discards the resumable phase state.

이동 모드는 먼저 파일을 복사하고 원본과 대상 내용을 바이트 단위로 비교합니다. 검증에 성공한 원본 파일만 삭제합니다. 오류가 발생하면 검증된 항목부터 이동, 모든 오류 해결 후 이동, 중단 또는 이번 실행에서 생성한 대상 파일 정리를 선택할 수 있습니다. 정리는 확인 후 실행하며 실행 전에 존재한 파일을 보존합니다. 이동·정리 단계의 일시정지는 남은 항목부터 재개하고, 중지는 해당 단계의 재개 상태를 폐기합니다.

### One-way synchronization / 단방향 동기화

Backup mode copies new or changed source files and lets you select whether to delete or keep destination-only items. Continuous mode uses a separate tray agent for multiple tasks, login startup, configurable scan intervals/time windows, battery and metered-network conditions, and exclusions/link options. Continuous mode always saves logs and enables restartable copies without automatic retries.

백업 모드는 원본의 신규·변경 파일을 반영하고 대상 전용 항목의 삭제·유지를 선택할 수 있습니다. 지속 모드는 별도 트레이 에이전트에서 복수 작업, 로그인 시 시작, 감시 간격·시간대, 배터리·종량제 네트워크 조건 및 예외·링크 옵션을 관리합니다. 지속 모드는 로그 저장과 재시작 가능한 복사를 항상 적용하며 자동 재시도는 사용하지 않습니다.

Choose to keep, remove, or review destination-only files in continuous mode. Automatically removed items are quarantined for seven days and can be restored; restored paths are retained by monitoring. Unavailable source/destination paths skip both copying and deletion. Overlapping task paths are blocked. Notifications and file-level copy/delete details are retained for seven days. The tray provides task editing, synchronization, settings, quarantine, and explicit exit controls.

지속 모드는 대상 전용 파일의 유지, 제거 또는 사용자 확인을 선택할 수 있습니다. 자동 제거 항목은 7일간 격리되어 복구할 수 있고, 복구 경로는 감시에서 유지합니다. 원본·대상 경로에 연결할 수 없으면 복사와 삭제를 모두 건너뜁니다. 작업 경로 중복은 차단합니다. 알림과 파일별 복사·삭제 상세 기록은 7일간 보관합니다. 트레이에서 작업 수정, 동기화, 설정, 격리소 및 명시적 종료를 관리합니다.

Continuous mode offers a low-thread recommendation reaching 70% of the best benchmark speed and a performance recommendation reaching 95%. This release implements one-way synchronization; two-way synchronization is not available.

지속 모드는 최고 측정 속도의 70% 이상인 후보 중 낮은 스레드 수를 추천하는 모드와 95% 기준 성능 추천 모드를 제공합니다. 이 버전은 단방향 동기화를 구현하며 양방향 동기화는 제공하지 않습니다.

### Errors, privileges, and safety / 오류·권한·안전성

Inspect failed files with error codes and paths, select individual files for non-recursive retries, and track retry state. Windows Restart Manager identifies locking processes. A normal shutdown request requires confirmation; forced termination becomes available only after normal shutdown fails and requires a separate explicit warning approval.

오류 코드와 경로로 실패 파일을 확인하고 선택 파일만 비재귀 재시도하며 상태를 추적할 수 있습니다. Windows Restart Manager로 잠금 프로세스를 확인합니다. 정상 종료 요청은 사용자 확인 후 수행하며, 강제 종료는 정상 종료 실패 후에만 제공하고 별도 경고창에서 명시적으로 승인해야 실행합니다.

Access-denied files can be retried through a separate Elevated Worker after UAC approval. The main GUI is not elevated and ACLs are not changed automatically. Identical and nested source/destination paths are rejected. Robocopy arguments use `ProcessStartInfo.ArgumentList`; exit codes are interpreted as bit flags, so a nonzero code does not automatically mean failure.

접근 거부 파일은 UAC 승인 후 별도 Elevated Worker에서 재시도할 수 있습니다. 메인 GUI의 권한을 올리거나 ACL을 자동 변경하지 않습니다. 원본·대상의 동일 경로 및 포함 관계는 차단합니다. Robocopy 인자는 `ProcessStartInfo.ArgumentList`로 전달하고 종료 코드는 비트 플래그로 해석하므로 0 이외의 코드가 모두 실패를 의미하지는 않습니다.

### Profiles, queues, and Explorer / 프로필·작업 큐·탐색기

Save named profiles containing paths, mode, threads, retries, exclusions, and link/copy options. Profiles support overwrite, rename, duplicate, and confirmed deletion. Queue jobs sequentially, add/edit/remove jobs during execution, pause safely, resume with `/Z`, and recover saved job state after restarting the application.

경로, 작업 방식, 스레드, 재시도, 예외 및 링크·복사 옵션을 이름 있는 프로필로 저장합니다. 덮어쓰기, 이름 변경, 복제 및 확인 후 삭제를 지원합니다. 작업은 큐에서 순서대로 실행하고 실행 중 추가·수정·삭제, 안전 일시정지, `/Z` 재개 및 앱 재시작 후 저장 상태 복구를 지원합니다.

Register or remove per-user Explorer context menus from the blue header. Multi-selected files/folders can be collected in the background; paste opens one application window and offers copy, move, backup, or continuous synchronization with immediate defaults or detailed settings. Selections from different parent folders become separate jobs. Windows 11 may place these classic context menus under **Show more options**.

파란색 헤더에서 사용자 계정별 탐색기 우클릭 메뉴를 등록하거나 해제할 수 있습니다. 복수 선택한 파일·폴더는 백그라운드에서 수집하며 붙여넣기 시 앱 창 하나를 열어 복사, 이동, 백업 또는 지속 동기화를 기본값으로 바로 시작하거나 세부 설정할 수 있습니다. 상위 폴더가 다른 선택 항목은 별도 작업으로 분리합니다. Windows 11에서는 기존 방식의 우클릭 메뉴가 **더 많은 옵션 표시**에 나타날 수 있습니다.

## Build and test / 빌드 및 테스트

Develop on Windows x64 using **.NET SDK 8.0.425**, as specified in [global.json](global.json). Run these commands from the repository root. The test suite covers argument boundaries including Korean/spaces/special characters, exit codes, path safety, verified moves, synchronization, state recovery, and UI/resource contracts. Windows integration tests also exercise the real `robocopy.exe` in temporary folders.

[global.json](global.json)에 지정된 **.NET SDK 8.0.425**를 사용해 Windows x64에서 개발하세요. 저장소 루트에서 다음 명령을 실행합니다. 테스트는 한글·공백·특수문자의 인자 경계, 종료 코드, 경로 안전성, 검증 이동, 동기화, 상태 복구 및 UI·리소스 계약을 확인합니다. Windows 통합 테스트는 임시 폴더에서 실제 `robocopy.exe`도 실행합니다.

```powershell
dotnet restore RoboCopy_GUI_Kr.sln --configfile NuGet.Config
dotnet build RoboCopy_GUI_Kr.sln -c Release --no-restore
dotnet test RoboCopy_GUI_Kr.sln -c Release --no-build --no-restore
dotnet run --project src\RoboCopy_GUI_Kr\RoboCopy_GUI_Kr.csproj
```

For a project-local SDK, run `Setup-LocalSdk.ps1`, then use `.\.dotnet\dotnet.exe` instead of `dotnet`. `Build.ps1` additionally creates a self-contained development package, compiled tests, helper tools, and a source snapshot; it also registers the development application's Explorer menu for the current user. Use the direct commands above when you only need build/test checks without Explorer registration.

프로젝트 내 SDK를 사용하려면 `Setup-LocalSdk.ps1`을 실행하고 `dotnet` 대신 `.\.dotnet\dotnet.exe`를 사용하세요. `Build.ps1`은 자체 포함 개발 패키지, 빌드된 테스트, 시험 도구 및 소스 스냅샷을 추가로 생성하고 현재 사용자의 탐색기 메뉴에 개발 앱을 등록합니다. 탐색기 등록 없이 빌드·테스트만 수행하려면 위의 직접 명령을 사용하세요.

```powershell
.\Setup-LocalSdk.ps1
.\Build.ps1
.\Build-Installer.ps1
```

The installer output is `artifacts\installer\RoboCopy_GUI_Kr_Setup_v1.0_x64.exe`. Build it on Windows with PowerShell and IExpress. Both online and offline payloads include project documentation and runtime license notices. Runtime DLLs account for most of the installer size.

설치기 출력은 `artifacts\installer\RoboCopy_GUI_Kr_Setup_v1.0_x64.exe`입니다. PowerShell과 IExpress를 갖춘 Windows에서 빌드하세요. 온라인·오프라인 페이로드 모두 프로젝트 문서와 런타임 라이선스 고지를 포함합니다. 설치기 용량의 대부분은 런타임 DLL입니다.

## Repository layout / 프로젝트 구성

```text
src/RoboCopy_GUI_Kr                 Main WPF interface / 메인 WPF UI
src/RoboCopy_GUI_Kr.Core            Copy and safety logic / 복사 및 안전성 로직
src/RoboCopy_GUI_Kr.Agent           Continuous-sync tray agent / 지속 동기화 에이전트
src/RoboCopy_GUI_Kr.ElevatedWorker  Selected elevated retries / 선택 항목 관리자 재시도
src/RoboCopy_GUI_Kr.LockTestHelper  Manual lock-test helper / 잠금 시험 도우미
tests/RoboCopy_GUI_Kr.Core.Tests   Unit and integration tests / 단위 및 통합 테스트
installer/                        Installer and license packaging / 설치 및 라이선스 동봉
test-assets/                      Manual test scripts and fixtures / 수동 시험 자료
```

## GitHub and GitLab / GitHub·GitLab

Upload source, tests, scripts, and documentation. `.gitignore` excludes build outputs, local SDK/NuGet caches, logs, generated test state, and signing keys. Publish installer binaries as release assets instead of committing them to Git. See [RELEASING.md](RELEASING.md) for the initial upload, matching tags on both hosts, release notes, and SHA-256 verification.

소스, 테스트, 스크립트 및 문서를 업로드하세요. `.gitignore`는 빌드 산출물, 로컬 SDK·NuGet 캐시, 로그, 생성된 시험 상태 및 서명 키를 제외합니다. 설치기 바이너리는 Git에 커밋하지 않고 릴리스 첨부 파일로 공개하세요. 최초 업로드, 두 서비스의 동일 태그, 릴리스 설명 및 SHA-256 확인은 [RELEASING.md](RELEASING.md)를 참고하세요.

[GitHub Actions](.github/workflows/build.yml) and [GitLab CI](.gitlab-ci.yml) restore, build, and test on Windows; they do not publish releases automatically. GitLab.com uses the `saas-windows-medium-amd64` runner tag; for self-managed GitLab, change it to a Windows PowerShell runner tag. Hosted CI execution can be verified after upload.

[GitHub Actions](.github/workflows/build.yml)와 [GitLab CI](.gitlab-ci.yml)는 Windows에서 복원·빌드·테스트를 수행하며 릴리스를 자동 공개하지 않습니다. GitLab.com은 `saas-windows-medium-amd64` 런너 태그를 사용합니다. 자체 운영 GitLab에서는 Windows PowerShell 런너의 태그로 변경하세요. 호스팅 서비스의 CI 실행은 업로드 후 확인할 수 있습니다.

## Logs and issue reports / 로그·문제 보고

Application logs are stored at `%LOCALAPPDATA%\RoboCopy_GUI_Kr\logs`. When opening an issue, include the app/Windows version, mode, expected and actual behavior, reproduction steps, and relevant error codes. Remove private paths, filenames, account names, and other sensitive information before attaching logs or screenshots.

앱 로그는 `%LOCALAPPDATA%\RoboCopy_GUI_Kr\logs`에 저장됩니다. 이슈에는 앱·Windows 버전, 작업 방식, 기대 결과와 실제 결과, 재현 방법 및 오류 코드를 포함하세요. 로그나 화면 이미지를 첨부하기 전에 개인 경로, 파일명, 계정명 등 민감한 정보를 지우세요.

The current interface is Korean. README, project license, release guidance, and release notes are provided paragraph by paragraph in English and Korean. Internal development documents and manual test instructions are currently Korean; see [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md), [DEVELOPMENT_TOOLS.md](DEVELOPMENT_TOOLS.md), and [the development plan](Robocopy_GUI_개발계획서.md).

현재 UI는 한국어입니다. README, 프로젝트 라이선스, 공개 절차 및 릴리스 설명은 단락별로 영문·한글을 함께 제공합니다. 내부 개발 문서와 수동 시험 설명은 현재 한국어로 제공하며 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md), [DEVELOPMENT_TOOLS.md](DEVELOPMENT_TOOLS.md), [개발 계획](Robocopy_GUI_개발계획서.md)을 참고하세요.

## License / 라이선스

Copyright (c) 2026 **ds2lie85**. Original project code and documentation are available under the **MIT License**: use, modification, and redistribution, including commercial use, are permitted subject to retaining the copyright and permission notices. See [LICENSE](LICENSE) for the full English text and Korean translation. Third-party components keep their own terms; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

저작권 (c) 2026 **ds2lie85**. 프로젝트의 독자적인 코드와 문서는 **MIT 라이선스**로 제공합니다. 저작권과 허가 고지를 유지하는 조건으로 상업적 사용을 포함한 사용·수정·재배포를 허용합니다. 영문 전문과 한글 번역은 [LICENSE](LICENSE), 제3자 구성 요소의 조건은 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 확인하세요.
