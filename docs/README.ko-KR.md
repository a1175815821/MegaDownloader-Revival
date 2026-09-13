# MegaDownloader 부활 계획

**언어**：[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja-JP.md) · **한국어**

> 고전 MEGA 다운로더를 다시 사용할 수 있게 합니다. v1.8 역컴파일 소스를 기반으로 수정되었으며, 60개 이상의 수정이 완료되었습니다.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../../stargazers)

---

## 소개

MegaDownloader는 스페인 개발자 **Andres Soliño**가 만든 MEGA 클라우드 다운로드 관리자로, 가볍고 안정적이며 멀티스레드를 지원하는 것으로 유명합니다. 원본 프로젝트는 v1.8 이후 유지 관리가 중단되었고, MEGA는 링크 형식을 변경했기 때문에(`mega.nz/file/...`, `mega.nz/folder/...`) 구버전은 새 링크를 인식하지 못해 핵심 기능이 동작하지 않았습니다.

이 저장소는 부활 계획입니다. v1.8을 역컴파일하여 얻은 소스를 기반으로 수정 및 리팩터링했습니다.

**현재 버전: v2.5.0**. 전체 변경 내역은 [CHANGELOG](CHANGELOG.ko-KR.md)를 참조하십시오.

> ⚠️ **법적 고지**: 이 프로젝트는 이미 배포된 타사 소프트웨어의 역컴파일에서 비롯되었으며, 목적은 호환성 문제를 수정하여 사용 가능성을 복원하는 데에만 있습니다. 원작자가 이 저장소가 권리를 침해한다고 판단하면 Issue를 통해 연락해 주십시오. 협조하여 처리하겠습니다.

---

## 빠른 시작

1. [Releases](../../../releases)에서 다운로드, 둘 중 하나 선택:
   - **`MegaDownloader-Revival-win-x86.zip`** —— 포터블 버전. 임의의 디렉터리에 압축 해제 후 `MegaDownloader.exe`를 더블클릭
   - **`MegaDownloader.exe`** —— 단일 파일 버전. 12개의 종속 DLL이 내장되어 있어 다운로드 후 압축 해제 없이 바로 더블클릭
2. MEGA 링크를 복사하면 프로그램이 클립보드 내용을 자동으로 인식합니다
3. 도구 모음의 **링크 추가**를 클릭하여 수동으로 붙여넣거나, 링크를 주 창으로 드래그할 수도 있습니다
4. **설정**에서 다운로드 디렉터리, 동시 연결 수, 속도 제한을 구성합니다
5. **설정 → 일반 → 테마**에서 다크/라이트 모드를 전환합니다(저장 후 즉시 적용)

### 링크 예시

새 형식(v1.9부터 지원):

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

구 형식(계속 지원):

```
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
https://mega.co.nz/#F!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
```

암호화 링크:

```
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
mega://enc2?abcDEFgh-IjklMNop
```

---

## 기능

| 기능 | 설명 |
| --- | --- |
| 멀티스레드 다운로드 | 하나의 파일에 여러 연결로 동시에 접속하여 속도를 크게 향상 |
| 이어받기 | 일시 중지, 다시 시작, 오류 재시도를 지원 |
| 속도 제한 | 전체 또는 단일 작업 속도 제한 |
| 클립보드 감시 | 클립보드에 복사된 MEGA 링크를 자동으로 인식 |
| 드래그 지원 | 링크를 주 창으로 드래그하면 대기열에 추가 |
| MEGA 폴더 | 공유 폴더 전체를 재귀적으로 파싱하여 다운로드, 지정한 하위 폴더/파일만 다운로드 지원 |
| 암호화 링크 | `enc` / `enc2` / `fenc` / `fenc2` / `elc` 여러 형식 지원 |
| ELC 컨테이너 | 암호화 링크 컨테이너 가져오기 및 내보내기 |
| 스트리밍 재생 | VLC 통합, 다운로드하면서 재생 |
| 웹 인터페이스 | HttpServer 내장, 브라우저로 원격 관리 가능, LAN 액세스 지원 |
| 스트리밍 라이브러리 | 스트리밍 리소스를 시각적으로 관리 |
| Stegano 스테가노그래피 | 이미지/비디오의 스테가노그래피 인코딩 및 디코딩 |
| 자동 압축 해제 | SharpCompress 기반, RAR / 7Z / ZIP 지원 |
| 할당량 서킷 브레이커 | MEGA 할당량 소진 시 자동으로 일시 중지하고 카운트다운 후 자동으로 복구(v2.5) |
| 실패 자동 복구 | 실패한 작업을 주기적으로 자동 재시도, 영구 실패는 자동 제외(v2.5) |
| 다국어 인터페이스 | 10개 언어 지원, 확장 가능 |
| 다크/라이트 테마 | 시스템을 따르거나 수동 전환, Auto 모드에서 실시간 추적 |

### 지원하는 링크 형식

- `mega.nz/#...!FileID!FileKey`(구버전)
- `mega.nz/file/FileID#FileKey`(신버전)
- `mega.nz/folder/FolderID#FolderKey`(신버전 폴더)
- MEGA URI 프로토콜: `mega://#!...`, `mega://enc?...`, `mega://elc?...`

> v2.0부터 종료된 다음 서비스에 대한 지원이 제거되었습니다. MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga, goo.gl 단축 링크.

---

## 빌드

### 환경 요구 사항

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK(Visual Studio 설치 시 포함)
- Windows 7 SP1 이상

### 빌드 구성

| 구성 | 설명 |
| --- | --- |
| `Debug` | 디버그 버전, `bin/Debug/`에 출력 |
| `Release` | 릴리스 버전, `bin/Release/`에 출력 |
| `Debug_MSD` | MegaSearchDesktop 통합 디버그 버전 |
| `Release_MSD` | MegaSearchDesktop 통합 릴리스 버전, `Resources/Installer MSD/`에 출력 |

### 단계

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# Visual Studio에서 MegaDownloader.sln을 연 후 Ctrl+Shift+B
```

명령줄 빌드:

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

출력물은 `bin/<Configuration>/MegaDownloader.exe`에 있습니다.

### 기술 스택

VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle(암호화) · Newtonsoft.Json · ObjectListView · SharpCompress(압축 해제) · HttpServer/Fadd(웹) · F5Lib(스테가노그래피)

---

## 프로젝트 구조

```
MegaDownloader/
├── Clases/                 # 핵심 클래스 라이브러리(암호화, 다운로드, 구성, 테마, 업데이트)
├── Controls/               # 사용자 지정 컨트롤
├── Forms/                  # WinForms 창(12개)
├── HttpModule/             # 내장 웹 서버 모듈 및 HTML 템플릿
├── Stegano/                # 스테가노그래피 창
├── Resources/
│   ├── DLLs/               # 타사 DLL 종속성
│   ├── Language/           # 다국어 XML(10개)
│   └── Installer MSD/      # WiX 설치 패키지 프로젝트
├── docs/                   # 문서(README, 변경 로그, 기여 가이드)
├── My Project/             # VS 프로젝트 메타데이터
└── MegaDownloader.sln
```

전체 디렉터리 트리와 파일 용도 설명은 [CONTRIBUTING](CONTRIBUTING.ko-KR.md)을 참조하십시오.

---

## 지원하는 언어

소프트웨어 인터페이스에 10개 언어가 내장되어 있습니다:

| 언어 | 파일 |
| --- | --- |
| English | `en-US-Language.xml` |
| Español | `es-ES-Language.xml` |
| 간체 중국어 | `zh-CN-Language.xml` |
| 번체 중국어 | `zh-TW-Language.xml` |
| Français | `fr-FR-Language.xml` |
| Deutsch | `de-DE-Language.xml` |
| Italiano | `it-IT-Language.xml` |
| Português (Brasil) | `pt-BR-Language.xml` |
| Magyar | `hu-HU-Language.xml` |
| Română | `ro-RO-Language.xml` |

언어를 추가하거나 기존 번역을 개선하려면 [CONTRIBUTING](CONTRIBUTING.ko-KR.md)을 참조하십시오.

---

## 문서

| 문서 | 내용 |
| --- | --- |
| [CHANGELOG](CHANGELOG.ko-KR.md) | 전체 버전 기록 및 버전별 수정 내역 |
| [CONTRIBUTING](CONTRIBUTING.ko-KR.md) | 기여 절차, 코드 스타일, 릴리스 절차 |

이 문서는 여러 언어로 제공됩니다. **모든 언어 버전은 수동으로 유지 관리됩니다**, 하나의 언어를 업데이트할 때 다른 언어도 함께 업데이트하십시오:

| 언어 | README | CHANGELOG | CONTRIBUTING | |
| --- | --- | --- | --- | --- |
| English | `docs/README.md` | `CHANGELOG.md` | `CONTRIBUTING.md` | 수동 유지 관리 |
| 간체 중국어 | `docs/README.zh-CN.md` | `CHANGELOG.zh-CN.md` | `CONTRIBUTING.zh-CN.md` | **공식 원본** |
| 번체 중국어 | `docs/README.zh-TW.md` | `CHANGELOG.zh-TW.md` | `CONTRIBUTING.zh-TW.md` | 수동 유지 관리 |
| 일본어 | `docs/README.ja-JP.md` | `CHANGELOG.ja-JP.md` | `CONTRIBUTING.ja-JP.md` | 수동 유지 관리 |
| 한국어 | `docs/README.ko-KR.md` | `CHANGELOG.ko-KR.md` | `CONTRIBUTING.ko-KR.md` | 수동 유지 관리 |

> 간체 중국어가 공식 원본이며, 원본 파일 자체가 간체 중국어 버전이므로 별도의 `README.zh-CN.md`가 없습니다.

---

## 감사의 말

- 원본 MegaDownloader 저자 **Andres Soliño**
- 부활 계획 유지 관리자 **Yingxue**(v2.0+)
- 오픈소스 종속성: [BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## 라이선스

[MIT License](LICENSE)에 따라 배포됩니다. 원본 저작권 © 2018 Andres Soliño, 부활 계획 수정 저작권 © 2026 MegaDownloader Revival Project 기여자.

> 저장소 내 타사 DLL은 각 원본 라이선스를 따르며, 사용자는 규정 준수를 직접 확인해야 합니다.
