# Windows 11 개발용 탐색기 메뉴

`Build.ps1`은 개발 실행 파일을 생성한 뒤 `ExplorerPackage/Register-ExplorerPackage.ps1`로 개발용 외부 위치 패키지를 현재 사용자에게 등록한다. 인스톨러는 빌드하지 않는다.

기본 우클릭 메뉴의 앱 영역에 `Robocopy로 복사`를 직접 클릭하는 항목으로 등록한다. 파일/폴더 선택 시 하위 메뉴를 펼치지 않고 원본을 선택한다. 대상 폴더의 빈 공간에는 `Robocopy로 붙여넣기` 하위 메뉴를 통해 붙여넣기·이동·백업·지속 동기화 명령을 제공한다. 레거시 메뉴의 선택 폴더 붙여넣기는 유지한다. 원본 선택 및 안전 이동/충돌 확인은 기존 앱 처리 흐름을 사용한다. 앱 간 순서는 Windows가 결정한다.

COM 확장은 기존 WPF 외부 서버에서 실행한다. Explorer 프로세스에 .NET 런타임을 로드하지 않는다. `IExplorerCommand`, `IEnumExplorerCommand`, `IObjectWithSite`를 제공하며 manifest의 `com:ExeServer`로 활성화한다.

등록/활성화 확인:

```powershell
$packageRoot = '.\artifacts\development\RoboCopy_GUI_Kr_Development\app\ExplorerPackage'
& "$packageRoot\Register-ExplorerPackage.ps1"
& "$packageRoot\Verify-ExplorerPackage.ps1"
```

개발용 느슨한 manifest 등록을 허용하지 않는 시스템에서는 등록이 실패할 수 있다. Build.ps1은 개발 폴더의 `ExplorerPackage-registration.log`에 기록하고 기존 레거시 메뉴를 유지한다. 개발자 모드·인증서 신뢰·탐색기 재시작은 자동 변경하지 않는다. 등록 후 Explorer가 캐시를 유지하면 로그아웃 후 다시 로그인한다.

수동 검증: 파일 한 개/여러 개/폴더 한 개/폴더 배경에서 기본 메뉴의 Robocopy 표시를 확인한다. 한글·공백·특수문자 원본 선택 후 새 대상 폴더에서 붙여넣기를 확인한다. 파일이나 여러 폴더 선택에서는 붙여넣기 명령이 표시되지 않아야 한다. 이동과 충돌 처리의 기존 확인 절차도 점검한다.

개발용 등록 제거:

```powershell
& "$packageRoot\Register-ExplorerPackage.ps1" -Unregister
```

## 무료 설치 옵션 (2026-10-10)

사용자 결정에 따라 유료 인증서/서명 서비스는 사용하지 않는다. 설치 소스에 `개발자 모드 활성화 — Windows 11 기본 우클릭 메뉴 등록용` 옵션을 추가했다. 기본 미체크이며 Windows 11에서만 표시된다. 경고에는 PC 전체 적용, 앱 설치 제한 완화, 예기치 못한 위험, 관리자 승인 필요, 제거 시 자동 비활성화되지 않음을 명시한다.

체크 후 설치 버튼을 누르면 추가 확인을 표시한다. 실제 설정 변경은 설치 파일 복사에 성공한 뒤 `Enable-DeveloperMode.ps1` 도우미만 UAC로 승격해 수행한다. 패키지는 원래 설치 사용자의 권한으로 등록한다. 승인 취소/실패 시 설치된 앱과 기존 레거시 메뉴는 유지하고 기본 메뉴 등록 실패를 알린다. 개발자 모드가 이미 활성화되어 있으면 체크하지 않아도 탐색기 연동 옵션에 따라 기본 메뉴를 등록할 수 있다.

설치본은 `RoboCopyGUIKr.Explorer`와 별도 CLSID를 사용하여 개발본과 구분한다. 제거 시 해당 설치 경로의 패키지만 제거하며 PC의 개발자 모드는 변경하지 않는다. 해당 옵션 개발 당시에는 인스톨러를 빌드하지 않았으며, 사용자 승인 후 1.1 인스톨러에 포함해 빌드했다.

이 컴퓨터에서는 사용자 승인에 따라 개발자 모드 활성화, 개발 패키지 등록 및 자동 COM 활성화가 성공했다. `Verify-ExplorerPackage.ps1 -SelectionPath <파일 또는 폴더>`로 실제 Shell 선택을 원격 COM에 전달해 하위 명령 활성 상태를 검사할 수 있다. 이 프로브는 복사/이동/동기화 작업을 실행하지 않는다.

참고: [Microsoft Windows 11 셸 메뉴 연동](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer), [com:ExeServer](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-com-exeserver).
