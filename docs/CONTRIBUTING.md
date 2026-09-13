# Contributing Guide

First of all, thank you for contributing to the MegaDownloader Revival project! This document walks you through the contribution workflow.

## Code of conduct

Please be friendly and respectful to all participants. We welcome any contribution related to the project's goal (making MegaDownloader usable again) — whether that's fixing bugs, adding features, improving translations, or refining documentation.

## What can I contribute?

| Type | Description |
| --- | --- |
| 🐛 Bug fixes | Fix link parsing, download failures, UI issues, and so on |
| ✨ New features | Support new crypters, new link protectors, new protocols, etc. |
| 🌐 Translation | Improve existing translations or add a language in `Resources/Language/` |
| 📚 Documentation | Improve README, CHANGELOG, and code comments |
| 🎨 UI/UX | Improve WinForms layout, icons, and usability |
| 🔧 Refactoring | Raise code quality without changing behaviour |

## Development environment

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK
- Git

## Project structure

```
MegaDownloader/
├── Clases/                         # Core class library
│   ├── Cryptography/AES.vb         #   AES encryption
│   ├── StreamingLibrary/           #   Streaming library management
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/network communication
│   ├── Configuracion.vb            #   Configuration management
│   ├── ConfiguracionUI.vb          #   UI configuration (themes, etc.) ★ new in v2.0
│   ├── FileDownloader.vb           #   File download core
│   ├── MegaFolderHelper.vb         #   MEGA folder parsing
│   ├── MegaURIProtocol.vb          #   mega:// protocol registration
│   ├── Mutex.vb                    #   Mutex (single-instance process)
│   ├── Paquete.vb                  #   Download package data
│   ├── ThrottledStream.vb          #   Rate-limited stream
│   ├── ThemeManager.vb             #   Theme manager ★ new in v2.0
│   ├── URLExtractor.vb             #   URL parsing
│   ├── URLProcessor.vb             #   URL processing
│   └── Updater.vb                  #   Auto-update
├── Controls/                       # Custom controls
│   └── ELCAccountControl.vb
├── HttpModule/                     # Built-in web server modules
│   ├── StreamingModule.vb
│   ├── StreamingLibraryModule.vb
│   ├── WebInterfaceModule.vb
│   └── Template/                   #   HTML templates
├── Stegano/                        # Steganography forms
│   ├── SteganoManager.vb
│   ├── SteganoWizardLoad.vb
│   └── SteganoWizardSave.vb
├── Resources/
│   ├── DLLs/                       # Third-party DLL dependencies
│   ├── Language/                   # Multi-language XML (10 languages)
│   └── Installer MSD/              # WiX installer project
├── My Project/                     # VS project metadata
├── Forms/                          # WinForms forms (12)
│   ├── Main.vb                     #   Main form
│   ├── AddLinks.vb                 #   Add-links form
│   ├── Configuration.vb            #   Settings form (includes theme switching)
│   ├── StreamingForm.vb            #   Streaming playback form
│   ├── Credits.vb                  #   About/credits
│   ├── SplashScreen.vb             #   Splash screen
│   ├── Cerrando.vb                 #   Closing screen
│   ├── Descompresor.vb             #   Extraction form
│   ├── ELCForm.vb                  #   ELC container form
│   ├── EncodeLinksForm.vb          #   Link encryption form
│   ├── PantallaMsg.vb              #   Message dialog form
│   └── PropiedadesDescarga.vb      #   Download properties form
├── docs/                           # Project documentation
│   ├── CHANGELOG.md                #   Changelog
│   └── CONTRIBUTING.md             #   Contributing guide
├── MegaDownloader.sln              # VS solution
├── MegaDownloader.vbproj           # VS project
├── app.config                      # .NET runtime configuration
├── ApplicationEvents.vb            # Application-level event handling
├── README.md                       # Project readme
├── LICENSE                         # MIT license
└── .gitignore                      # Git ignore rules
```

### Key directories

| Directory | Contents |
| --- | --- |
| `Clases/` | Core class library: cryptography (`Cryptography/`), streaming library (`StreamingLibrary/`), HTTP communication (`Conexion.vb`), configuration (`Configuracion.vb`), download core (`FileDownloader.vb`), folder parsing (`MegaFolderHelper.vb`), themes (`ThemeManager.vb`), updating (`Updater.vb`) |
| `Forms/` | WinForms forms. The main form is `Main.vb`, which holds most of the download-list and theme logic |
| `HttpModule/` | Built-in web server modules; `Template/` holds the HTML templates |
| `Stegano/` | Steganography forms and logic |
| `Resources/Language/` | UI localization XML, one file per language |
| `Resources/DLLs/` | Third-party dependency DLLs, committed with the repository |
| `docs/` | This document and the changelog |

## Contribution workflow

### 1. Fork and clone the repository

```bash
# After forking the repository to your own GitHub account:
git clone https://github.com/<your-username>/MegaDownloader-Revival.git
cd MegaDownloader
git remote add upstream https://github.com/a1175815821/MegaDownloader-Revival.git
```

### 2. Create a feature branch

```bash
# Branch off the latest main
git checkout main
git pull upstream main
git checkout -b feature/your-feature-name
# or: fix/bug-description, docs/topic, i18n/language-improvement
```

### 3. Develop and test locally

- Open `MegaDownloader.sln` in Visual Studio
- Build with the `Debug` configuration
- Run `bin/Debug/MegaDownloader.exe` to verify your changes

**Test cases (please cover these):**

- Legacy link: `https://mega.nz/#!abcDEF!ghijklmnop`
- New link: `https://mega.nz/file/abcDEF#ghijklmnop`
- Folder link: `https://mega.nz/folder/abcDEF#ghijklmnop`
- Encrypted link: `mega://enc?...`
- Clipboard auto-detection
- Drag & drop of links

### 4. Commit your changes

Follow the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification:

```
<type>(<scope>): <subject>

<optional body>

<optional footer>
```

Common types:

- `feat`: new feature, e.g. `feat(url): support mega.nz/embed/ links`
- `fix`: bug fix, e.g. `fix(clipboard): restore broken clipboard monitoring`
- `docs`: documentation, e.g. `docs: fill in the zh-CN translation`
- `refactor`: refactoring, e.g. `refactor(conexion): simplify proxy configuration`
- `i18n`: translation, e.g. `i18n(zh-CN): complete untranslated entries`

```bash
git add .
git commit -m "feat(url): support mega.nz/embed/ links"
```

### 5. Push and open a PR

```bash
git push origin feature/your-feature-name
```

Open a Pull Request against `main` on GitHub. In the description, explain:

- What does this PR change?
- Why is the change needed? (reference the Issue number)
- How can it be tested?
- Does it affect existing functionality?

### 6. Review and merge

A maintainer will review your PR and may request changes. Please be patient — every change is in service of the project's long-term maintainability.

## Code style conventions

- The VB.NET project has `Option Strict On`, `Option Explicit Off`, and `Option Infer On` enabled — **new code must satisfy these constraints**
- File encoding: **UTF-8 with BOM**
- Indentation: **4 spaces**
- Naming:
  - Classes and methods: PascalCase, e.g. `ExtraerFileID`
  - Private fields: camelCase or an underscore prefix, e.g. `_ProxyIP`
  - Local variables: camelCase, e.g. `fileInfo`
- Comments:
  - Non-obvious logic needs a `'` single-line comment
  - Public APIs use `''' <summary>` XML doc comments
- The original project uses Spanish names such as `Clases`, `Configuracion`, `Fichero`. **New code may use English names** to stay consistent, but do not mass-rename existing identifiers

## Adding a new Crypter / Link Protector

Follow the pattern in [`Clases/Crypters/EncrypterMega.vb`](../Clases/Crypters/EncrypterMega.vb):

1. Create `<Name>.vb` under `Clases/Crypters/`
2. Implement `ObtenerInformacionFichero`, returning `Conexion.InformacionFichero`
3. In [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb):
   - Add a `<NAME>TOKEN` constant
   - Add a matching regex to `patternOthers`
   - Add a branch in `ExtraerFileID`
4. Wire up the UI in `Forms/Main.vb` if needed

## Documentation and translation

This project maintains **two independent multilingual systems** — one for documentation and one for the app UI. Contributions to either are welcome.

### Documentation languages (README / CHANGELOG / CONTRIBUTING)

Documentation follows a "Chinese source → automatic translation" model:

- **Only the Chinese source files are hand-maintained**: `docs/README.zh-CN.md`, `docs/CHANGELOG.zh-CN.md`, `docs/CONTRIBUTING.zh-CN.md`
- English and the other languages (Traditional Chinese / Japanese / Korean) are generated automatically by GitHub Actions — **do not edit the generated files by hand**
- To improve wording, edit the Chinese source file, or open a PR that modifies it
- To add a language, edit `i18n/config.yml`; see [i18n/README.md](../i18n/README.md) for details

### App UI languages

UI strings live in `Resources/Language/<locale>-Language.xml`, one file per language.

To improve a translation:

1. Find the XML for the target language (e.g. `ja-JP-Language.xml`)
2. Edit the CDATA content of the `<Text>` nodes — **do not change the `key` attribute**
3. If you add a key, make sure `en-US-Language.xml` also has a key with the same name (it serves as the fallback baseline)

### Adding a new UI language

1. Copy `Resources/Language/en-US-Language.xml` to `<locale>-Language.xml`
2. Translate the CDATA content of every `<Text>` node
3. Register the embedded resource in `MegaDownloader.vbproj`:

```xml
<EmbeddedResource Include="Resources\Language\ja-JP-Language.xml" />
```

4. Run the app — the new language appears under **Settings → Language**

> Language key lookup falls back in three stages: disk file → embedded resource → `en-US` → the key itself. So even if some keys are missing, the app will not crash — it just shows English or the raw key.

## Reporting bugs

When filing a bug, please include the following in the Issue:

- **MegaDownloader version** (see About → Version)
- **Windows version**
- **Link type** (paste a full example link; you may redact sensitive parts)
- **Steps to reproduce**
- **Expected behaviour** vs **actual behaviour**
- **Error log** (if any — located in the application directory)

## Release process (maintainers only)

1. Confirm all tests pass and that both `Debug` and `Release` build
2. Update `docs/CHANGELOG.zh-CN.md` with the new version section (documentation is translated automatically once pushed)
3. Update `AssemblyVersion` and `AssemblyFileVersion` in `My Project/AssemblyInfo.vb`
4. Update `VERSION_MEGADOWNLOADER` and `VERSION_UPDATE` in `Resources/InternalConfig.xml` (Base64-encoded)
5. Update `<Version>` in `docs/version.xml`
6. Write bilingual release notes (format described in section 6 of [i18n/README.md](../i18n/README.md))
7. Create and push a Git tag — CI builds the artifacts and creates the GitHub Release automatically:

```bash
git tag -a v2.5.0 -m "Release v2.5.0"
git push origin main
git push origin v2.5.0
```

On a `v*` tag, CI automatically:
- Builds the Release configuration
- Verifies that all 12 embedded DLLs are present in the single-file build (the build fails if any are missing, so we never ship a "double-click does nothing" package)
- Packages the zip and uploads it to the GitHub Release

> For test pre-releases (RC builds), publish as `prerelease` first and promote to a full release once verified.

## Contact

- Filing an Issue: GitHub Issues
- Security-related matters: please do not discuss them in a public Issue — contact the maintainers by email instead

---

Thank you again for contributing! Let's bring MegaDownloader back to life. 🚀
