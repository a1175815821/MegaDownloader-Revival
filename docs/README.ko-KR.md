<p align="center">
  <img src="screenshots/banner.png" alt="MegaDownloader 부활 프로젝트 — 클래식 MEGA 다운로더를 오늘의 링크에 맞게 다시 만들다" width="100%">
</p>

<h1 align="center">MegaDownloader 부활 프로젝트</h1>

<p align="center">
  <b>클래식 MEGA 다운로더를 오늘의 링크에 맞게 다시 만들었습니다.</b><br>
  멀티스레드 전송 · 확실한 이어받기 · 할당량 자동 복구 · 다운로드 전 일괄 확인
</p>

<p align="center">
  <a href="../../../releases/latest"><img alt="최신 릴리스 다운로드" src="https://img.shields.io/badge/Download-Latest%20Release-FF6B1F?style=for-the-badge&logo=windows&logoColor=white"></a>
  <a href="CHANGELOG.md"><img alt="변경 내역" src="https://img.shields.io/badge/Changelog-2.6.1-2D333B?style=for-the-badge&logo=gitbook&logoColor=white"></a>
</p>

<p align="center"><sub>도움이 되셨다면 <a href="https://github.com/a1175815821/MegaDownloader-Revival">⭐ Star</a>로 프로젝트를 응원해 주세요.</sub></p>

<p align="center">
  <a href="../../../releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?style=flat-square&label=release&color=FF6B1F"></a>
  <a href="../../../releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total?style=flat-square&label=downloads&color=4E8BFF"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%207%20SP1%2B-0078D4?style=flat-square">
  <a href="../LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-3DA639?style=flat-square"></a>
  <a href="../../../stargazers"><img alt="Stars" src="https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=flat-square&label=stars&color=FFC107"></a>
</p>

<p align="center">
  <sub>단일 EXE 또는 ZIP · 설치 불필요 · 관리자 권한 불필요 · .NET Framework 4.8 · UI 10개 언어</sub><br>
  <sub><a href="../README.md">English</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.ja-JP.md">日本語</a> · <b>한국어</b></sub>
</p>

> **v2.6.1: 정돈된 화면, 빠르게 열리는 설정, 시스템 언어 자동 선택.** 라이트/다크 화면의 계층을 정리하고 첫 실행 메뉴를 수정했습니다. Windows 표시 언어에 맞는 번역이 없으면 영어를 사용합니다. [업데이트 보기 ↓](#-261-새로운-기능)
>
> MEGA(MEGA Ltd.)와 무관한 비공식 프로젝트입니다.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="screenshots/main-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="screenshots/main-light.png">
    <img alt="MegaDownloader v2.6.1 workbench" src="screenshots/main-light.png" width="100%">
  </picture>
</p>
<p align="center"><sub>v2.6.1 워크벤치 · 스크린샷은 GitHub 테마에 맞춰 바뀝니다. 화면 언어는 중국어 간체입니다.</sub></p>

---

## ✨ 2.6.1 새로운 기능

- **더 명확한 화면 구성** — 사이드바, 글꼴, 여백, 버튼을 통일하고 진행률 막대와 백분율을 한 열로 합쳤습니다. 상세 정보가 창 너비에 맞춰 이동하며 정지 버튼의 글자와 예약 다운로드의 시계 아이콘을 표시합니다.
- **처음부터 모든 메뉴 표시** — 파일, 옵션, 스트리밍, 도움말이 크기 변경 없이 보입니다. 설정 버튼도 사이드바 왼쪽에 맞췄습니다.
- **빠르게 열리는 설정** — 반복 레이아웃 계산을 줄이고 ELC 계정 편집기는 해당 탭을 열 때 불러옵니다. 격리 테스트의 첫 표시가 약 8.2초에서 약 1.1초로 줄었습니다. 실제 시간은 환경에 따라 다릅니다.
- **Windows 표시 언어 따르기** — 새 설정은 자동 선택이 기본입니다. 지역별 언어와 중국어 간체/번체를 구분하고 지원하지 않는 언어는 영어를 사용합니다. 기존 수동 선택은 유지합니다. 설정의 일반 탭에서 변경한 뒤 저장하고 재시작하세요. 한국어 UI 번역은 기본 제공하지 않습니다.
- **스크린샷과 회귀 검사 갱신** — 아래 이미지는 2.6.1의 실제 네이티브 UI와 데모 데이터입니다. 전체 수정 사항과 첫 업그레이드 시 6열 배치로의 이전은 [CHANGELOG](CHANGELOG.md)를 참고하세요.

## 2.6에서 도입한 워크벤치 기능

> **창은 하나, 단계는 둘, 추측은 없습니다.**
>
> ⚡ **콜드 스타트 실측 5~7초 → 150~200ms** · 500개 넘는 파일 가져오기도 멈추지 않음
>
> 시작 시간은 하드웨어와 실행 환경에 따라 다릅니다.

<table>
<tr>
<td width="50%" valign="top">

<b>🖥️ 워크벤치 재작업</b>

라이트/다크가 같은 여백, 글자 크기, 아이콘 그리드를 공유합니다. "링크 추가"가 드디어 주 동작이 되었고, 사이드바 그룹이 각자 개수를 표시하며, 세부 정보 패널은 접어서 목록에 폭을 넘겨줄 수 있습니다.

</td>
<td width="50%" valign="top">

<b>➕ 2단계 추가</b>

같은 창에서 링크를 붙여 넣고 파일을 고릅니다. 대화상자가 겹치지 않습니다. 뒤로 가도 링크, 검색어, 펼친 폴더, 체크 상태가 그대로 남습니다.

</td>
</tr>
<tr>
<td valign="top">

<b>🗂️ 일괄 사전 확인</b>

다운로드 전에 파일 이름, 크기, 상태, 상대 경로를 확인합니다. 폴더 트리는 접을 수 있고, 이름 검색, 형식 필터, Ctrl/Shift 다중 선택, 선택 합계 크기와 대상 디스크 여유 공간을 표시합니다.

</td>
<td valign="top">

<b>🔎 작업 검색</b>

<kbd>Ctrl</kbd>+<kbd>F</kbd>로 패키지 이름, 파일 이름, 상대 경로를 검색합니다. 현재 상태 그룹과 함께 필터링되고 <kbd>Esc</kbd>로 지우면 전체 큐가 복원됩니다.

</td>
</tr>
<tr>
<td valign="top">

<b>⏰ 예약 다운로드</b>

한 번만 예약하거나 매일 시간대를 지정합니다. 자정을 넘는 구간도 지원하며, 시간대가 시작되면 자동 재개하고 끝나면 일시정지합니다. 설정은 재시작 후에도 유지됩니다.

</td>
<td valign="top">

<b>⚡ 200ms 시작</b>

콜드 스타트가 5~7초에서 150~200ms로 줄었습니다. 첫 화면이 더 이상 성능 카운터와 WMI를 동기로 읽지 않습니다. 링크 분석, 패키지 구성, 메타데이터 확인도 UI 스레드에서 분리했고 사전 확인 목록은 가상화했습니다.

</td>
</tr>
</table>

<details>
<summary><b>2.6의 나머지 변경과 그 뒤의 수정들</b></summary>

<br>

- **중복 감지**: 파일 ID + 키로 신·구 링크 형식의 중복을 식별하고 기존 큐와도 대조합니다. 중복과 무효 항목은 항상 건너뜁니다.
- **일시적 실패와 영구 실패 분리**: 네트워크가 잠깐 끊겨 확인하지 못한 일반 링크는 직접 선택해 추가할 수 있습니다.
- **확인 중 들어온 새 링크**는 다음 배치로 보류하며 몰래 다운로드하지 않습니다.
- **대형 패키지**(500개 파일 초과)는 기본으로 접힌 상태로 시작해 가져오는 순간 목록이 멈추지 않습니다.
- **추가 후** 자동으로 전체 작업으로 전환해 최신 패키지를 보여주고, 파일 트리는 보이는 행의 강조와 스크롤 위치를 유지합니다.
- **진행 상황을 보고 멈출 수 있음**: 분석 중 경과 시간과 중지/재확인, 제출 시 "다운로드 큐에 추가" 상태를 따로 표시합니다.
- **DPI·테마 수정**: 고DPI 폴더 펼침 표식, 세부 정보 스크롤 후 레이아웃 밀림, 네이티브 목록 배경과 빈 상태 테마 적용, 서식 있는 붙여넣기의 웹 글자색 유입.
- **예약으로 큐 관리**: 설정한 시간대에 다운로드를 재개하고 종료 시 일시정지합니다. 전체 큐를 수동 일시정지하면 다음 시간대까지 유지됩니다.

</details>

---

## ⚡ 30초 만에 시작

1. [Releases](../../../releases/latest)에서 둘 중 하나를 받습니다:

   | 파일 | 이런 경우에 |
   | --- | --- |
   | **`MegaDownloader.exe`** (단일 파일) | 가장 간단하게: 받아서 더블클릭. 의존 DLL 15개가 내장되어 있습니다 |
   | **`MegaDownloader-Revival-win-x86.zip`** (포터블) | 압축 파일을 선호한다면: 풀어서 실행 |

2. **실행** — 설치 없음, 관리자 권한 없음
   > Windows 7 SP1 이상 · .NET Framework 4.8 (Windows 10 1903+ 기본 포함, 이전 버전은 안내에 따라 설치)

3. **MEGA 링크 복사** — 클립보드 감시가 인식하고, 확인하면 큐에 추가됩니다

이게 전부입니다. 다른 방법: 창에 링크 끌어다 놓기 · 툴바의 "링크 추가" · 브라우저에서 `mega://` 링크 클릭.

---

## 🚀 무엇을 하는가

**다운로드 엔진**

- **멀티스레드** — 파일 하나에 여러 연결로 대역폭을 끝까지 사용
- **제대로 되는 이어받기** — 일시정지·재개·오류 재시도, 재시작 후에도 이어서, 중단된 부분 파일 재사용
- **폴더 다운로드** — 공유 폴더를 재귀적으로 분석하고 필요한 하위 폴더나 파일만 선택
- **속도 제한** — 전체 또는 작업 단위
- **실패 자가 복구** — 실패한 작업을 주기적으로 재시도하고 영구 실패는 제외 (v2.5)
- **할당량 차단기** — MEGA 할당량이 떨어지면 자동 일시정지 후 카운트다운, 시간이 되면 자동 재개. 기존 "할당량 돌파" 모드는 기본 꺼짐 상태로 유지 (v2.5)
- **자동 압축 해제** — 완료 시 RAR / 7Z / ZIP 해제, 분할·암호 압축 지원, 같은 이름의 기존 파일은 덮어쓰지 않음
- **결과 검증** — 링크에 MetaMAC이 있으면 다운로드한 파일을 검증한 뒤 완료로 표시 (v2.5.9)

**링크 가져오기**

- **네 가지 방법** — 클립보드 감시 · 끌어다 놓기 · 직접 붙여넣기 · `mega://` 프로토콜
- **일괄 사전 확인** — 큰 공유도 내용을 보고 고르기 (v2.6)
- **ELC 컨테이너** — 암호화 링크 컨테이너 가져오기/내보내기. `enc` / `enc2` / `fenc` / `fenc2` / `elc` 모두 지원

**워크벤치**

- **즉시 시작** — 콜드 스타트 150~200ms, WMI와 성능 카운터 동기 읽기 없음 (v2.6)
- **작업 검색** — 패키지·파일·경로를 `Ctrl+F`로 (v2.6)
- **예약 다운로드** — 매일 시간대 또는 한 번 예약 (v2.6)
- **세부 정보 접기** — 큐 통계, 작업 목록, 세부 정보 3단 구성. 좁은 창에서는 자동으로 폭을 양보
- **다크/라이트 테마** — 시스템 설정을 따르거나 직접 지정
- **UI 10개 언어** — English · Español · 简体中文 · 繁體中文 · Français · Deutsch · Italiano · Português (BR) · Magyar · Română
- **웹 인터페이스** — 내장 HTTP 서버. 브라우저에서 작업 추가와 진행 확인, LAN 접근을 켜면 휴대폰에서도 전송 가능

**부가 기능**

- **받으면서 재생** — VLC를 통합해 아직 받는 중인 영상 스트리밍 재생
- **스트리밍 라이브러리** — 자주 보는 스트리밍 소스를 시각적으로 관리
- **스테가노그래피** — 이미지·영상 안에 파일 숨기기 (F5)

<details>
<summary><b>지원하는 모든 링크 형식</b></summary>

<br>

- 신형: `mega.nz/file/ID#KEY`, `mega.nz/folder/ID#KEY` (v1.9+), 공유 내 하위 폴더·단일 파일 링크 포함
- 구형: `mega.nz/#!ID!KEY`, `mega.co.nz/#F!ID!KEY`
- 암호화: `mega://enc?…`, `mega://enc2?…`, `mega://fenc?…`, `mega://fenc2?…`, `mega://elc?…`

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
```

> v2.0에서 종료된 서비스 지원을 제거했습니다: MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga, goo.gl 단축 링크.

</details>

---

## 🖼️ 인터페이스

**링크부터 다운로드까지 두 단계**

<table>
<tr>
  <td align="center" width="50%"><b>1. 링크 붙여넣기</b><br><img src="screenshots/add-links.png" alt="링크 입력 창" width="100%"></td>
  <td align="center" width="50%"><b>2. 미리 확인하고 파일 선택</b><br><img src="screenshots/preflight.png" alt="사전 확인 및 파일 선택 창" width="100%"></td>
</tr>
</table>

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="screenshots/settings-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="screenshots/settings-general.png">
    <img alt="MegaDownloader 2.6.1 settings" src="screenshots/settings-general.png" width="85%">
  </picture>
</p>
<p align="center"><sub>2.6.1 설정 · 언어 자동 선택과 스크롤 가능한 구성. 데모 데이터를 사용했으며 화면은 중국어 간체입니다。</sub></p>

---

## 📖 이 프로젝트의 유래

MegaDownloader는 스페인 개발자 **Andres Soliño**가 만든 MEGA 다운로드 관리자로, 가볍고 안정적이며 멀티스레드를 지원하는 것으로 알려져 있습니다. 원 프로젝트는 v1.8 이후 유지보수가 중단되었고, MEGA가 링크 형식(`mega.nz/file/...`, `mega.nz/folder/...`)을 바꾸면서 구버전은 새 링크를 인식하지 못해 핵심 기능이 동작하지 않게 되었습니다.

이 저장소는 그것을 되살린 결과물입니다. v1.8을 디컴파일해 소스를 복원하고, 그 위에서 수정과 리팩터링을 이어왔습니다. 지금까지 **200건 이상의 수정과 강화**(크래시, 조용한 데이터 손실, 보안, 호환성)를 적용했고 시맨틱 버전 규칙으로 꾸준히 배포합니다.

|  | 원본 v1.8 (2018년 중단) | **이 저장소 v2.6.1** |
| --- | --- | --- |
| 신형 `mega.nz/file` / `/folder` 링크 | ❌ 인식 못 함 | ✅ v1.9부터 지원 |
| MEGA 할당량 소진 | 멈춤·오류 | ✅ 자동 정지 + 카운트다운 재개 (v2.5) |
| 실패한 작업 | 수동 재시도만 | ✅ 주기적 자가 복구 (v2.5) |
| 큰 공유 추가 | 붙여넣고 기도 | ✅ 일괄 사전 확인 + 폴더 트리 (v2.6) |
| 작업 찾기 | 큐 전체 스크롤 | ✅ 패키지·파일·경로 검색 (v2.6) |
| 무인 다운로드 | — | ✅ 매일 시간대 또는 한 번 예약 (v2.6) |
| 시작 속도 | 첫 화면이 블로킹됨 | ✅ 콜드 스타트 150~200ms (기존 5~7초, v2.6) |
| UI 테마 | 시스템 회색뿐 | ✅ 다크/라이트, 시스템 추종 (v2.0) |
| 안정성 | — | ✅ 200건 이상의 크래시·데이터 손실·보안 수정 |
| 유지보수 | 중단 | ✅ 활발히 유지, Issue 응답 |

> ⚠️ **법적 고지**: 이 프로젝트는 제3자가 배포한 소프트웨어를 디컴파일한 결과에서 출발했으며, 호환성 문제를 고쳐 다시 사용할 수 있게 하는 것만을 목적으로 합니다. 원저자가 권리 침해라고 판단하면 Issue로 알려 주세요. 협력해 처리하겠습니다.

---

## ❓ 자주 묻는 질문

**할당량 초과(quota exceeded)라고 나옵니다.**
MEGA는 무료 계정과 IP에 다운로드 할당량을 겁니다. v2.5부터는 자동으로 일시정지하고 카운트다운 후 재개합니다. 기다릴 수 없다면 설정에서 "할당량 돌파 모드"를 켜 계속 재시도할 수 있습니다.

**설정과 다운로드 큐는 어디에 있나요?**
`%LOCALAPPDATA%\MegaDownloader\`에 있습니다. 프로그램 폴더는 자유롭게 옮기거나 지워도 설정에 영향이 없습니다. Windows를 다시 설치하기 전에 이 폴더를 백업하세요.

**구형 링크도 되나요?**
됩니다. 신·구 형식, `mega://` 암호화 링크, ELC 컨테이너를 모두 지원합니다(위 목록 참고).

**관리자 권한이 필요한가요?**
아니요. 포터블이며 권한 상승 없이 실행됩니다.

**Mac / Linux 버전이 있나요?**
없습니다. WinForms 기반이라 Windows 전용입니다. 다만 내장 웹 인터페이스를 켜면 어떤 기기의 브라우저에서든 다운로드를 관리할 수 있습니다.

**예약 다운로드가 PC를 깨우나요?**
아니요. 앱이 실행 중이어야 하며, 깨우기 타이머를 등록하거나 서비스로 상주하지 않습니다.

**보안 문제를 발견하면?**
공개 Issue를 열지 말고 [SECURITY](../SECURITY.md)의 절차로 알려 주세요.

---

## 🛠️ 개발자용

<details>
<summary><b>소스에서 빌드</b></summary>

<br>

**요구 사항**: Visual Studio 2019 또는 2022 · .NET Framework 4.8 개발자 팩 · Windows 7 SP1+

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# Visual Studio에서 MegaDownloader.sln을 열고 Ctrl+Shift+B
```

명령줄 빌드:

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

출력은 `bin/<Configuration>/MegaDownloader.exe`입니다. 빌드 구성: `Debug` / `Release` / `Debug_MSD` / `Release_MSD`(MegaSearchDesktop 통합판, `Resources/Installer MSD/`로 출력).

회귀 테스트: PowerShell에서 `.\Tests\Run-RegressionTests.ps1` 실행(.NET SDK 필요). 임시 파일과 로컬 HTTP 서버를 사용하므로 MEGA 계정이 필요 없습니다. 자세한 내용은 [Tests/README.md](../Tests/README.md).

**기술 스택**: VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle(암호) · Newtonsoft.Json · ObjectListView · SharpCompress(압축 해제) · HttpServer/Fadd(웹) · F5Lib(스테가노그래피)

</details>

<details>
<summary><b>프로젝트 구조</b></summary>

<br>

```
MegaDownloader/
├── Clases/                 # 핵심 클래스(암호, 다운로드, 설정, 테마, 사전 확인, 예약)
├── Controls/               # 사용자 정의 컨트롤
├── Forms/                  # WinForms 폼(워크벤치, 2단계 추가 흐름 등)
├── HttpModule/             # 내장 웹 서버 모듈과 HTML 템플릿
├── Stegano/                # 스테가노그래피 폼
├── Resources/
│   ├── DLLs/               # 서드파티 DLL 의존성
│   ├── Language/           # 로컬라이즈 XML(10개 언어)
│   └── Installer MSD/      # WiX 설치 프로젝트
├── docs/                   # 문서, 스크린샷
├── Tests/                  # 다운로드·압축 해제·스트리밍 회귀 테스트
└── MegaDownloader.sln
```

전체 디렉터리 트리는 [CONTRIBUTING](CONTRIBUTING.md)을 참고하세요.

</details>

---

## 📚 문서

| 문서 | 내용 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 전체 버전 기록과 버전별 수정 상세(중국어 간체, 원본) |
| [CONTRIBUTING](CONTRIBUTING.md) | 기여 절차, 코드 스타일, 릴리스 절차 |
| [SECURITY](../SECURITY.md) | 취약점 신고 방법 |
| [CODE OF CONDUCT](../CODE_OF_CONDUCT.md) | 커뮤니티 행동 강령 |
| [2.6.1 릴리스 노트](https://github.com/a1175815821/MegaDownloader-Revival/releases/tag/v2.6.1) | v2.6.1 주요 변경 사항과 다운로드 |

README는 5개 언어로 직접 관리합니다. CONTRIBUTING은 영어와 중국어 간체, CHANGELOG는 중국어 간체만 있습니다. 하나를 고치면 나머지도 함께 갱신해 주세요:

| 언어 | README | CHANGELOG | CONTRIBUTING |
| --- | --- | --- | --- |
| English | `README.md`(루트) | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 简体中文 | `docs/README.zh-CN.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.zh-CN.md` |
| 繁體中文 | `docs/README.zh-TW.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 日本語 | `docs/README.ja-JP.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 한국어 | `docs/README.ko-KR.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |

---

## ⭐ 프로젝트 지원하기

MegaDownloader Revival이 도움이 되었다면, 다음 방법이 가장 효과적입니다:

- ⭐ **Star 눌러주기** — 프로젝트가 더 눈에 띄게 하는 가장 직접적인 신호입니다
- 🐛 [Issue 올리기](../../../issues) — 최근 수정의 상당수가 사용자 제보에서 나왔습니다
- 🌍 [번역](CONTRIBUTING.md) 개선 — 10개 언어가 있지만 원어민 교정이 부족합니다
- 🔗 아직 쓸 만한 MEGA 다운로더를 찾는 사람에게 알려주기

<a href="https://star-history.com/#a1175815821/MegaDownloader-Revival&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
 </picture>
</a>

---

## 감사의 말

- 원 MegaDownloader 개발자 **Andres Soliño**
- 부활 프로젝트 메인테이너 **Yingxue**(v2.0+), 그리고 Issue와 번역으로 기여한 모든 분
- 오픈소스 의존성: [BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/)

## 라이선스

[MIT License](../LICENSE)로 배포합니다. 원본 저작권 © 2018 Andres Soliño, 부활 프로젝트 수정에 대한 저작권 © 2026 MegaDownloader Revival Project 기여자.

> 저장소의 서드파티 DLL은 각자의 원래 라이선스를 따릅니다. 사용자가 직접 준수 여부를 확인해야 합니다.
