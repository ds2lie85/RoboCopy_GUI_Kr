# Publishing to GitHub and GitLab / GitHub 및 GitLab 공개

The project is licensed under MIT, copyright (c) 2026 ds2lie85. Use one local Git history with two remotes so both services receive the same source commit and version tag. These instructions prepare and publish version 1.1; repository URLs must be filled in by the owner.

프로젝트는 MIT 라이선스이며 저작권자는 (c) 2026 ds2lie85입니다. 하나의 로컬 Git 이력에 원격 저장소 두 개를 연결하여 두 서비스에 같은 소스 커밋과 버전 태그를 올리세요. 이 문서는 버전 1.1의 준비 및 공개 절차이며 저장소 URL은 소유자가 입력해야 합니다.

## 1. Create empty repositories / 빈 저장소 생성

### Same-version rebuild / 동일 버전 재빌드

The October 12, 2026 rebuild keeps version 1.1 and the existing filenames. Prepare the final source commit, installer, source ZIP, release notes and checksums together in `artifacts/release/v1.1.0`. If `v1.1.0` already exists remotely, inspect its commit and the host's release first; do not force-move a published tag. Publish a clearly dated rebuild through the owner's chosen release policy. Replace older downloads and checksums together so they do not describe different builds.

2026-10-12 재빌드는 버전 1.1과 기존 파일명을 유지합니다. 최종 소스 커밋, 설치기, 소스 ZIP, 릴리스 설명과 체크섬을 `artifacts/release/v1.1.0`에 함께 준비하세요. 원격에 `v1.1.0` 태그가 이미 있다면 연결된 커밋과 릴리스를 먼저 확인하고 공개 태그를 강제로 이동하지 마세요. 소유자의 릴리스 정책에 따라 날짜를 명시한 재빌드를 공개하고 이전 다운로드와 체크섬을 함께 갱신하세요. 이번 로컬 작업은 외부 업로드를 수행하지 않습니다.

Create an empty `RoboCopy_GUI_Kr` repository on GitHub and GitLab. Do not generate a README, license, or .gitignore on either service because those files already exist locally. Use your actual repository URLs below; `ds2lie85` is the copyright attribution and does not necessarily identify a service account.

GitHub와 GitLab에서 빈 `RoboCopy_GUI_Kr` 저장소를 만드세요. README, 라이선스 및 .gitignore는 로컬에 있으므로 서비스에서 자동 생성하지 마세요. 아래에는 실제 저장소 URL을 사용하세요. `ds2lie85`는 저작권 표기이며 서비스의 계정명과 같다는 가정은 하지 않습니다.

## 2. Verify the upload set / 업로드 항목 확인

Run the following from the project root (or the extracted clean source package). If a Git repository already exists, skip `git init`. Review staged files before committing: include source, tests, installer scripts, manual-test scripts/fixtures, CI configuration, LICENSE, and documentation. SDK caches, binaries, artifacts, logs, generated destinations/lock markers, and credentials must remain excluded. `.gitignore` does not remove files that were already tracked.

프로젝트 루트 또는 정리된 소스 묶음을 푼 폴더에서 다음 명령을 실행하세요. 이미 Git 저장소라면 `git init`은 건너뜁니다. 커밋 전에 스테이징 파일을 확인하세요. 소스, 테스트, 설치 스크립트, 수동 시험 스크립트·자료, CI 설정, LICENSE 및 문서를 포함합니다. SDK 캐시, 바이너리, 빌드 산출물, 로그, 생성된 대상·잠금 상태 및 인증 정보는 제외해야 합니다. `.gitignore`는 이미 추적 중인 파일을 자동으로 제거하지 않습니다.

```powershell
git init -b main
git add .
git status --short
git diff --cached --stat
git diff --cached --check
```

## 3. Build, test, and create the installer / 빌드·테스트·설치기 생성

Use the SDK version specified by `global.json` on Windows x64. These direct build/test commands do not register Explorer menus. To use the local SDK, first run `Setup-LocalSdk.ps1` and replace `dotnet` with `.\.dotnet\dotnet.exe`. A new installer build includes the project license and the runtime packages' original notices; use this rebuilt installer for the public release.

Windows x64에서 `global.json`에 지정된 SDK를 사용하세요. 아래 직접 빌드·테스트 명령은 탐색기 메뉴를 등록하지 않습니다. 로컬 SDK를 사용하려면 `Setup-LocalSdk.ps1` 실행 후 `dotnet`을 `.\.dotnet\dotnet.exe`로 바꾸세요. 새 설치기는 프로젝트 라이선스와 런타임 패키지의 원본 고지를 포함하므로 공개 릴리스에는 다시 빌드한 설치기를 사용하세요.

```powershell
dotnet restore RoboCopy_GUI_Kr.sln --configfile NuGet.Config
dotnet build RoboCopy_GUI_Kr.sln -c Release --no-restore
dotnet test RoboCopy_GUI_Kr.sln -c Release --no-build --no-restore
.\Build-Installer.ps1
.\Prepare-Release.ps1
```

Compute SHA-256 after the final build (and after signing, if used). Publish the same installer and checksum file on both services. Compare with `Get-FileHash` when downloading. Record the actual tested Windows environment and results in the release description; do not claim broader coverage than tested.

최종 빌드 후 SHA-256을 계산하세요. 코드 서명을 한다면 서명 후 계산합니다. 두 서비스에 동일한 설치기와 체크섬 파일을 공개하고 다운로드 후 `Get-FileHash`로 비교하세요. 실제 시험한 Windows 환경과 결과를 릴리스 설명에 기록하고, 시험하지 않은 환경까지 확인했다고 표기하지 마세요.

```powershell
$installer = 'artifacts\installer\RoboCopy_GUI_Kr_Setup_v1.1_x64.exe'
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  RoboCopy_GUI_Kr_Setup_v1.1_x64.exe" |
    Set-Content -LiteralPath artifacts\installer\SHA256SUMS.txt -Encoding ascii
```

## 4. Commit and upload the same history / 동일한 이력 커밋·업로드

Configure your own Git author identity before committing if necessary. Add the two remotes once, then push `main` to each. If a remote already exists, inspect `git remote -v` and reuse or intentionally update it rather than adding a duplicate. Replace the two URL placeholders before running these commands. Do not put access tokens in remote URLs; authenticate with the host's supported credential mechanism.

필요하면 커밋 전에 본인의 Git 작성자 정보를 설정하세요. 원격 저장소 두 개를 한 번 등록하고 각각 `main`을 올립니다. 원격이 이미 있다면 `git remote -v`로 확인하여 재사용하거나 의도적으로 변경하고 중복 등록하지 마세요. 실행 전에 두 URL 자리표시자를 바꾸세요. 원격 URL에 토큰을 넣지 말고 서비스가 지원하는 인증 방식을 사용하세요.

```powershell
git commit -m "Prepare RoboCopy_GUI_Kr 1.1 public release"
git remote add github <YOUR_GITHUB_REPOSITORY_URL>
git remote add gitlab <YOUR_GITLAB_REPOSITORY_URL>
git push -u github main
git push gitlab main
```

Check GitHub Actions and GitLab CI for this commit. GitLab CI targets the GitLab.com Windows runner `saas-windows-medium-amd64`; a self-managed service needs a Windows runner and an appropriate tag. The CI jobs build and test; they do not create or publish a release.

해당 커밋의 GitHub Actions 및 GitLab CI 결과를 확인하세요. GitLab CI는 GitLab.com의 Windows 런너 `saas-windows-medium-amd64`를 사용합니다. 자체 운영 서비스에는 Windows 런너와 해당 태그가 필요합니다. CI 작업은 빌드·테스트를 수행하며 릴리스를 생성하거나 공개하지 않습니다.

## 5. Tag and publish / 태그·릴리스 공개

After checks pass, tag the tested commit as `v1.1.0` and push the same tag to both services. On each host, create a release for this tag, use [the bilingual release notes](docs/releases/v1.1.0.md), and attach `RoboCopy_GUI_Kr_Setup_v1.1_x64.exe` plus `SHA256SUMS.txt`. GitLab assets can be uploaded to its package registry/project uploads and linked from the release. Source archives are generated by the hosting services from the tag.

검증 통과 후 시험한 커밋에 `v1.1.0` 태그를 만들고 두 서비스에 같은 태그를 올리세요. 각 서비스에서 해당 태그의 릴리스를 만들고 [영문·한글 릴리스 설명](docs/releases/v1.1.0.md)을 사용하여 `RoboCopy_GUI_Kr_Setup_v1.1_x64.exe`와 `SHA256SUMS.txt`를 첨부하세요. GitLab은 패키지 레지스트리 또는 프로젝트 업로드에 올린 파일을 릴리스에 연결할 수 있습니다. 소스 압축 파일은 서비스가 태그를 기준으로 자동 생성합니다.

```powershell
git tag -a v1.1.0 -m "RoboCopy_GUI_Kr 1.1"
git push github v1.1.0
git push gitlab v1.1.0
```
