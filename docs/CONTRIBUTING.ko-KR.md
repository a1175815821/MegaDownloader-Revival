# 기여 가이드

먼저, MegaDownloader 부활 프로젝트에 기여해 주셔서 감사합니다! 이 문서는 기여 절차를 안내합니다.

## 행동 강령

친절하게 모든 참여자를 존중해 주세요. MegaDownloader를 다시 사용 가능하게 한다는 프로젝트 목표와 관련된 기여라면 Bug 수정, 새 기능 추가, 번역 개선, 문서 개선 등 모두 환영합니다.

## 어떤 기여를 할 수 있나요?

| 유형 | 설명 |
| --- | --- |
| 🐛 Bug 수정 | 링크 구문 분석, 다운로드 실패, UI 오류 등의 수정 |
| ✨ 새 기능 | 새로운 Crypter, 새로운 링크 보호기, 새로운 프로토콜 등의 지원 |
| 🌐 번역 | `Resources/Language/` 내 기존 번역 개선 또는 새 언어 추가 |
| 📚 문서 | README, CHANGELOG, 코드 주석 개선 |
| 🎨 UI/UX | WinForms 화면 레이아웃, 아이콘, 사용성 개선 |
| 🔧 리팩터링 | 기능에 영향 없이 코드 품질 향상 |

## 개발 환경

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK
- Git

## 프로젝트 구조

```
MegaDownloader/
├── Clases/                         # 핵심 클래스 라이브러리
│   ├── Cryptography/AES.vb         #   AES 암호화
│   ├── StreamingLibrary/           #   스트리밍 라이브러리 관리
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/네트워크 통신
│   ├── Configuracion.vb            #   구성 관리
│   ├── ConfiguracionUI.vb          #   UI 구성(테마 등)★ v2.0 신규
│   ├── FileDownloader.vb           #   파일 다운로드 핵심
│   ├── MegaFolderHelper.vb         #   MEGA 폴더 구문 분석
│   ├── MegaURIProtocol.vb          #   mega:// 프로토콜 등록
│   ├── Mutex.vb                    #   뮤텍스(프로세스 단일 인스턴스)
│   ├── Paquete.vb                  #   다운로드 패키지 데이터
│   ├── ThrottledStream.vb          #   속도 제한 스트리밍
│   ├── ThemeManager.vb             #   테마 관리자 ★ v2.0 신규
│   ├── URLExtractor.vb             #   URL 구문 분석
│   ├── URLProcessor.vb             #   URL 처리
│   └── Updater.vb                  #   자동 업데이트
├── Controls/                       # 사용자 지정 컨트롤
│   └── ELCAccountControl.vb
├── HttpModule/                     # 내장 Web 서버 모듈
│   ├── StreamingModule.vb
│   ├── StreamingLibraryModule.vb
│   ├── WebInterfaceModule.vb
│   └── Template/                   #   HTML 템플릿
├── Stegano/                        # 스테가노그래피 폼
│   ├── SteganoManager.vb
│   ├── SteganoWizardLoad.vb
│   └── SteganoWizardSave.vb
├── Resources/
│   ├── DLLs/                       # 타사 DLL 종속성
│   ├── Language/                   # 다국어 XML(10종)
│   └── Installer MSD/              # WiX 설치 패키지 프로젝트
├── My Project/                     # VS 프로젝트 메타데이터
├── Forms/                          # WinForms 폼(12개)
│   ├── Main.vb                     #   메인 폼
│   ├── AddLinks.vb                 #   링크 추가 폼
│   ├── Configuration.vb            #   설정 폼(테마 전환 포함)
│   ├── StreamingForm.vb            #   스트리밍 재생 폼
│   ├── Credits.vb                  #   정보/감사
│   ├── SplashScreen.vb             #   시작 화면
│   ├── Cerrando.vb                 #   종료 화면
│   ├── Descompresor.vb             #   압축 해제 폼
│   ├── ELCForm.vb                  #   ELC 컨테이너 폼
│   ├── EncodeLinksForm.vb          #   링크 암호화 폼
│   ├── PantallaMsg.vb              #   메시지 알림 폼
│   └── PropiedadesDescarga.vb      #   다운로드 속성 폼
├── docs/                           # 프로젝트 문서
│   ├── README*.md                   #   프로젝트 설명(5개 언어)
│   ├── CHANGELOG*.md                #   변경 로그(5개 언어)
│   └── CONTRIBUTING*.md             #   기여 가이드(5개 언어)
├── MegaDownloader.sln              # VS 솔루션
├── MegaDownloader.vbproj           # VS 프로젝트
├── app.config                      # .NET 런타임 구성
├── ApplicationEvents.vb            # 응용 프로그램 수준 이벤트 처리
├── README.md                       # 프로젝트 설명
├── LICENSE                         # MIT 라이선스
└── .gitignore                      # Git 무시 규칙
```

### 핵심 디렉터리 설명

| 디렉터리 | 내용 |
| --- | --- |
| `Clases/` | 핵심 클래스 라이브러리: 암호화(`Cryptography/`), 스트리밍 라이브러리(`StreamingLibrary/`), HTTP 통신(`Conexion.vb`), 구성(`Configuracion.vb`), 다운로드 핵심(`FileDownloader.vb`), 폴더 구문 분석(`MegaFolderHelper.vb`), 테마(`ThemeManager.vb`), 업데이트(`Updater.vb`) |
| `Forms/` | WinForms 폼. 메인 폼은 `Main.vb`이며 다운로드 목록과 테마 로직을 다수 포함 |
| `HttpModule/` | 내장 Web 서버 모듈, `Template/` 아래는 HTML 템플릿 |
| `Stegano/` | 스테가노그래피 관련 폼 및 로직 |
| `Resources/Language/` | UI 다국어 XML, 언어당 하나의 파일 |
| `Resources/DLLs/` | 타사 종속 DLL, 저장소와 함께 커밋됨 |
| `docs/` | 본 문서 및 변경 로그 |

## 기여 절차

### 1. Fork 및 저장소 복제

```bash
# 자신의 GitHub 계정으로 Fork한 후:
git clone https://github.com/<사용자 이름>/MegaDownloader-Revival.git
cd MegaDownloader
git remote add upstream https://github.com/a1175815821/MegaDownloader-Revival.git
```

### 2. 기능 브랜치 생성

```bash
# 최신 main 브랜치에서 생성
git checkout main
git pull upstream main
git checkout -b feature/기능 이름
# 또는: fix/bug-설명, docs/문서 주제, i18n/언어-개선
```

### 3. 개발 및 로컬 테스트

- Visual Studio에서 `MegaDownloader.sln` 열기
- `Debug` 구성으로 빌드
- `bin/Debug/MegaDownloader.exe` 실행하여 변경 사항 검증

**테스트 케이스(반드시 포함하세요):**

- 이전 버전 링크:`https://mega.nz/#!abcDEF!ghijklmnop`
- 새 버전 링크:`https://mega.nz/file/abcDEF#ghijklmnop`
- 폴더 링크:`https://mega.nz/folder/abcDEF#ghijklmnop`
- 암호화 링크:`mega://enc?...`
- 클립보드 자동 인식
- 링크 드래그

### 4. 코드 커밋

[Conventional Commits](https://www.conventionalcommits.org/zh-hans/v1.0.0/) 규격을 따르세요:

```
<type>(<scope>): <subject>

<body선택>

<footer선택>
```

자주 사용하는 유형:

- `feat`: 새 기능, 예: `feat(url): 支持 mega.nz/embed/ 链接格式`
- `fix`: Bug 수정, 예: `fix(clipboard): 修复剪贴板监听失效问题`
- `docs`: 문서, 예: `docs: 补充 zh-CN 翻译`
- `refactor`: 리팩터링, 예: `refactor(conexion): 简化代理设置逻辑`
- `i18n`: 번역, 예: `i18n(zh-CN): 补全未翻译条目`

```bash
git add .
git commit -m "feat(url): 支持 mega.nz/embed/ 链接格式"
```

### 5. 푸시 및 PR 생성

```bash
git push origin feature/기능 이름
```

GitHub에서 `main` 브랜치로 Pull Request를 생성하고, PR 설명에 다음을 기재하세요:

- 이 PR은 무엇을 변경했나요?
- 왜 변경이 필요한가요?(관련 Issue 번호)
- 어떻게 테스트하나요?
- 기존 기능에 영향이 있나요?

### 6. 코드 리뷰 및 병합

유지관리자가 PR을 리뷰하고 수정을 요청할 수 있습니다. 프로젝트의 장기적인 유지보수성을 위한 것이니 인내심을 갖고 협조해 주세요.

## 코드 스타일 규칙

- VB.NET 프로젝트에는 `Option Strict On`, `Option Explicit Off`, `Option Infer On`이 활성화되어 있습니다. **새 코드는 반드시 이러한 제약 조건을 만족해야 합니다**
- 파일 인코딩:**UTF-8 with BOM**
- 들여쓰기:**4칸 공백**
- 명명:
  - 클래스, 메서드: PascalCase, 예: `ExtraerFileID`
  - 프라이빗 필드: camelCase 또는 밑줄 접두사, 예: `_ProxyIP`
  - 지역 변수: camelCase, 예: `fileInfo`
- 주석:
  - 복잡한 로직은 `'` 한 줄 주석으로 설명 필요
  - 공개 API는 `''' <summary>` XML 문서 주석 사용
- 원래 프로젝트는 `Clases`, `Configuracion`, `Fichero` 등 스페인어 명명을 사용합니다. **일관성을 위해 새 코드는 영어 명명을 사용할 수 있습니다**만 기존 식별자를 일괄적으로 바꾸지 마세요

## 새로운 Crypter / Link Protector 추가

[`Clases/Crypters/EncrypterMega.vb`](../Clases/Crypters/EncrypterMega.vb) 구현 패턴을 참조하세요:

1. `Clases/Crypters/` 아래에 `<Name>.vb` 새로 만들기
2. `ObtenerInformacionFichero` 메서드를 구현하고 `Conexion.InformacionFichero` 반환
3. [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 내에서:
   - `<NAME>TOKEN` 상수 추가
   - `patternOthers`에 일치 정규식 추가
   - `ExtraerFileID`에 분기 추가
4. 필요 시 `Forms/Main.vb`에서 UI에 연결

## 문서 및 번역

본 프로젝트의 **문서**와 **소프트웨어 UI**는 독립적인 두 개의 다국어 체계이며 모두 기여를 환영합니다.

### 문서 다국어(README / CHANGELOG / CONTRIBUTING)

문서는 수동 번역 모드를 채택하며 자동 번역 흐름은 없습니다:

- 각 언어의 문서는 사람이 직접 유지관리합니다: 영어(`docs/README.md`, `docs/CHANGELOG.md`, `docs/CONTRIBUTING.md`), 간체 중국어(`docs/*.zh-CN.md`, 권위 원본), 번체/일본어/한국어 대응 파일
- 문서 수정 시 모든 언어 버전을 동기화하여 내용을 일치시키세요
- 문서 표현을 개선하고 싶다면 → 해당 언어 파일을 직접 수정하거나 PR 제출
- 새 언어를 추가하고 싶다면 → 영어(또는 중국어) 문서를 복사하여 새 언어 파일로 만들고 각 문서 상단의 언어 탐색 링크와 언어 대조표를 업데이트하세요

### 소프트웨어 UI 다국어

UI 문구는 `Resources/Language/<locale>-Language.xml`에 있으며 언어당 하나의 파일입니다.

개선 단계:

1. 해당 언어의 XML 찾기(예: `ja-JP-Language.xml`)
2. `<Text>` 노드의 CDATA 내용을 수정하고 `key` 특성은 **변경하지 마세요**
3. 새 키를 추가해야 하는 경우 `en-US-Language.xml`에 동일한 이름의 key가 있는지 확인하세요(폴백 기준으로 사용)

### 새 UI 언어 추가

1. `Resources/Language/en-US-Language.xml`을 `<locale>-Language.xml`로 복사
2. 모든 `<Text>` 노드의 CDATA 내용 번역
3. `MegaDownloader.vbproj`에 포함 리소스 추가:

```xml
<EmbeddedResource Include="Resources\Language\ja-JP-Language.xml" />
```

4. 프로그램을 실행하고 **설정 → 언어**에서 새 언어가 보이는지 확인

> 언어 키 조회에는 3단계 폴백이 있습니다: 디스크 파일 → 내장 리소스 → `en-US` → key 자체 반환. 따라서 일부 키가 번역되지 않아도 프로그램이 충돌하지 않고 영어 또는 원래 key가 표시될 뿐입니다.

## Bug 보고

Bug 보고 시 Issue에 다음 정보를 포함해 주세요:

- **MegaDownloader 버전**(정보 → 버전에서 확인)
- **Windows 버전**
- **링크 유형**(샘플 링크 전체 복사, 민감한 부분은 마스킹 가능)
- **재현 단계**
- **예상 동작** vs **실제 동작**
- **오류 로그**(있는 경우, 프로그램 디렉터리 내 로그 파일)

## 릴리스 절차(유지관리자 전용)

1. 모든 테스트 통과 확인, `Debug` 및 `Release` 구성 모두 빌드 가능
2. `docs/CHANGELOG.zh-CN.md` 업데이트하여 새 버전 장 추가 및 다른 언어의 CHANGELOG 동기화 업데이트
3. `My Project/AssemblyInfo.vb` 내 `AssemblyVersion` 및 `AssemblyFileVersion` 업데이트
4. `Resources/InternalConfig.xml`(Base64 인코딩) 내 `VERSION_MEGADOWNLOADER` 및 `VERSION_UPDATE` 업데이트
5. `docs/version.xml` 내 `<Version>` 업데이트
6. 중영 이중 언어 릴리스 노트 작성(중영 대조, 중국어 먼저 작성 후 영어 작성, 과거 `release-notes-*.md` 구조 참조)
7. Git Tag 생성 및 푸시, CI가 자동으로 결과물을 빌드하고 GitHub Release 생성:

```bash
git tag -a v2.5.0 -m "Release v2.5.0"
git push origin main
git push origin v2.5.0
```

CI는 tag `v*`에서 자동으로:
- Release 구성 빌드
- 단일 파일 버전의 12개 내장 DLL이 모두 있는지 체크섬 검증(누락 시 빌드 실패, 「더블클릭해도 반응 없음」 패키지 방지)
- zip으로 패키징하여 GitHub Release에 업로드

> 사전 릴리스(RC 버전) 테스트는 먼저 `prerelease` 형태로 릴리스하고 검증 통과 후 정식 버전으로 전환하는 것을 권장합니다.

## 연락처

- Issue 제출:GitHub Issues
- 보안 관련 문제: 공개 Issue에서 논의하지 말고 이메일로 유지관리자에게 연락하세요

---

다시 한번 기여에 감사드립니다! 함께 MegaDownloader에 새 생명을 불어넣읍시다. 🚀
