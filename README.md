# Lilly Integrated Mod Manager

RimWorld 1.7용 통합 모드 관리자 프로젝트 기본 구조입니다.

## 현재 포함된 항목
- RimWorld 모드 기본 폴더 구조
- `About/About.xml`
- Visual Studio용 C# 프로젝트 파일
- 지정된 RimWorld 및 Harmony DLL 참조
- `build.bat` 빌드 스크립트
- 향후 C# 구현을 위한 `Source` 폴더

## 아직 구현하지 않은 항목
UI 및 게임 동작을 포함한 C# 코드는 아직 작성하지 않았습니다.
이번 단계는 프로젝트/모드 폴더와 빌드 환경만 준비합니다.

## 빌드
1. RimWorld 및 Harmony DLL 경로가 실제 설치 경로와 일치하는지 확인합니다.
2. `build.bat`을 실행합니다.
3. C# 소스가 추가되면 빌드 결과 DLL이 `Mod/Assemblies`에 생성됩니다.

빌드에는 .NET Framework 4.7.2 개발자 팩과 MSBuild(Visual Studio 또는 Build Tools)가 필요합니다.

## 설치 위치
완성된 모드 폴더를 다음 위치에 배치하도록 구성했습니다.

`Y:\SteamLibrary\steamapps\common\RimWorld\Mods\Lilly.IntegratedModManager`
