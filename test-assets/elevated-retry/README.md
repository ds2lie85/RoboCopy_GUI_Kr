# 관리자 권한 재시도 시험 환경

이 시험은 기존 시스템 폴더의 권한을 건드리지 않고 두 가지 오류 코드 5(접근 거부) 상황을 만든다.

- `관리자 전용 폴더`: 대상 디렉터리 전체를 보호하여 디렉터리 관리자 재시도를 검증한다.
- `파일 권한 시험\권한 재시도 파일.txt`: 부모 디렉터리는 일반 권한으로 접근할 수 있지만 대상 파일 하나만 보호하여 파일 관리자 재시도를 검증한다.

## 1. 시험 파일 준비

프로젝트 루트에서 **관리자 PowerShell**을 열고 다음을 실행한다. 이 단계만 관리자 권한으로 실행하며, 실제 검증에 사용할 RoboCopy GUI는 일반 권한으로 실행해야 한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\test-assets\elevated-retry\Setup-ElevatedRetry-Test.ps1"
```

스크립트가 출력한 다음 경로를 프로그램에 지정한다.

- 원본: `test-assets\elevated-retry\source`
- 대상: `test-assets\elevated-retry\destination`

## 2. 프로그램에서 확인

1. 재시도 횟수와 재시도 간격을 `0`으로 설정하고 복사를 시작한다.
2. `일반 파일.txt`는 복사되는지 확인한다.
3. `관리자 전용 폴더`의 디렉터리 오류와 `파일 권한 시험\권한 재시도 파일.txt`의 파일 오류가 각각 발생하는지 확인한다.
4. `오류 파일` 창에서 두 항목의 오류 코드가 `5`이고 설명 툴팁이 접근 거부로 표시되는지 확인한다.
5. 파일 오류만 선택하여 `관리자 권한으로 다시 시도`를 실행하고 파일 하나만 해결되는지 확인한다.
6. 디렉터리 오류를 별도로 관리자 재시도하여 해당 디렉터리 하위가 해결되는지 확인한다.
7. 두 오류를 함께 선택했을 때 UAC가 한 번만 표시되고 두 항목이 모두 해결되는지도 확인한다.

## 3. 시험 후 권한 복원

복원이 거부되면 **관리자 PowerShell**을 열고 다음을 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\test-assets\elevated-retry\Reset-ElevatedRetry-Test.ps1"
```

이 스크립트는 보호한 대상 디렉터리와 대상 파일의 권한 상속만 복원하며 파일을 자동 삭제하지 않는다.
