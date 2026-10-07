# Third-party notices / 제3자 구성 요소 고지

The MIT license in [LICENSE](LICENSE) covers the original RoboCopy_GUI_Kr code and documentation by ds2lie85. It does not replace the licenses of Microsoft or other dependency authors. Original third-party legal notices are retained verbatim in binary distributions.

[LICENSE](LICENSE)의 MIT 라이선스는 ds2lie85가 작성한 RoboCopy_GUI_Kr의 코드와 문서에 적용됩니다. Microsoft 및 기타 의존성 저작자의 라이선스를 대체하지 않습니다. 제3자의 법적 고지 원문은 바이너리 배포물에 그대로 보존합니다.

## Windows and Robocopy / Windows 및 Robocopy

`robocopy.exe`, Windows Shell, and Restart Manager are Windows components used from the installed operating system. This project does not bundle `robocopy.exe` or grant rights to redistribute Windows. Windows components remain subject to Microsoft's applicable terms.

`robocopy.exe`, Windows Shell, Restart Manager는 설치된 운영체제에서 사용하는 Windows 구성 요소입니다. 이 프로젝트는 `robocopy.exe`를 포함하지 않으며 Windows 재배포 권한을 부여하지 않습니다. Windows 구성 요소에는 Microsoft의 해당 이용 조건이 적용됩니다.

## .NET runtime distribution / .NET 런타임 배포

The installer contains a framework-dependent application and a self-contained application with .NET and Windows Desktop runtime packs. `installer/Copy-DistributionNotices.ps1` reads the published application's dependency manifest and copies the actual runtime packages' license and third-party notice files from the NuGet cache into `third-party/<package>/<version>/`. Both installer payloads receive these files along with the project's LICENSE, README, and this notice.

설치기는 런타임을 별도로 사용하는 앱과 .NET 및 Windows Desktop 런타임 팩을 포함한 자체 포함 앱을 함께 제공합니다. `installer/Copy-DistributionNotices.ps1`는 게시된 앱의 의존성 명세를 읽고 실제 사용한 런타임 패키지의 라이선스와 제3자 고지 파일을 NuGet 캐시에서 `third-party/<패키지>/<버전>/`으로 복사합니다. 설치기의 두 페이로드 모두 해당 파일과 프로젝트의 LICENSE, README 및 이 고지를 포함합니다.

The runtime packs currently resolved by SDK 8.0.425 use their package-provided MIT license texts. Keep the actual package notices when updating the SDK or runtime. Microsoft's separately downloaded Windows Desktop Runtime installer has its own terms; consult the [.NET licensing overview](https://github.com/dotnet/core/blob/main/license-information.md) and the [.NET Library License](https://dotnet.microsoft.com/en-us/dotnet_library_license).

SDK 8.0.425가 현재 선택하는 런타임 팩은 패키지에 제공된 MIT 라이선스 문구를 사용합니다. SDK 또는 런타임을 갱신할 때에도 실제 패키지의 고지를 유지하세요. 별도로 다운로드하는 Microsoft Windows Desktop Runtime 설치기에는 자체 이용 조건이 적용됩니다. [.NET 라이선스 안내](https://github.com/dotnet/core/blob/main/license-information.md)와 [.NET Library License](https://dotnet.microsoft.com/en-us/dotnet_library_license)를 참고하세요.

## Development dependencies / 개발 의존성

The test project directly references Microsoft.NET.Test.Sdk 17.8.0, xUnit 2.5.3, xunit.runner.visualstudio 2.5.3, and coverlet.collector 6.0.0. These are development dependencies and are not part of the installer application payload. Restoring packages retrieves their own license metadata and transitive dependencies. This file is an overview, not a replacement for package license texts.

테스트 프로젝트는 Microsoft.NET.Test.Sdk 17.8.0, xUnit 2.5.3, xunit.runner.visualstudio 2.5.3, coverlet.collector 6.0.0을 직접 참조합니다. 이들은 개발 의존성이며 설치기의 앱 페이로드에는 포함되지 않습니다. 패키지 복원 시 각 패키지의 라이선스 메타데이터와 전이 의존성도 내려받습니다. 이 문서는 개요이며 패키지의 라이선스 원문을 대체하지 않습니다.
