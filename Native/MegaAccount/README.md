# MEGA account module

This native library connects the application to the official [MEGA SDK v10.21.1](https://github.com/meganz/sdk/releases/tag/v10.21.1). A prebuilt Windows x64 copy lives in `prebuilt/x64/`, so a normal Windows build ships the account feature without extra tooling; the application can also build and download anonymously when the library is absent.

## Prebuilt copies

| Platform | Library | Distribution |
| --- | --- | --- |
| Windows x64 | `prebuilt/x64/MegaAccountBridge.dll` | `MegaAccountBridge.dll` beside the executable, with `msvcp140.dll`, `vcruntime140.dll` and `vcruntime140_1.dll` |
| macOS arm64 | `prebuilt/osx-arm64/libMegaAccountBridge.dylib` | produced by `.github/workflows/native-account.yml`, placed in `Contents/MacOS` or beside the executable |
| Linux x64 | `prebuilt/linux-x64/libMegaAccountBridge.so` | produced by `.github/workflows/native-account.yml`, placed beside the executable |

`MegaDownloader.Core` probes `MEGADOWNLOADER_NATIVE_DIR`, the executable directory, `native/` and `runtimes/<rid>/native/`, then the macOS bundle's `Frameworks`/`Resources` folders. Keep the [SDK license](https://github.com/meganz/sdk/blob/v10.21.1/LICENSE) with the binaries; the prebuilt folders include `MEGA-SDK-LICENSE.txt`.

## Build

Obtain the SDK at tag `v10.21.1` separately and a vcpkg checkout with the SDK's `builtin-baseline` (`ef7dbf94b9198bc58f45951adcf1f041fcbc5ea0`).

Windows (Visual Studio 2022 with desktop C++):

```powershell
cmake -S Native/MegaAccount -B bin/MegaAccountNative -G 'Visual Studio 17 2022' -A x64 `
  -DMEGA_SDK_DIR=C:/src/mega-sdk -DVCPKG_ROOT=C:/src/vcpkg -DUSE_OPENSSL=OFF
cmake --build bin/MegaAccountNative --config Release --target MegaAccountBridge
```

`USE_OPENSSL=OFF` is the supported Windows configuration: the SDK's curl uses Schannel, so no OpenSSL dependency is needed. The build uses the `x64-windows-static-md` triplet by default.

macOS arm64:

```bash
cmake -S Native/MegaAccount -B build/mega-account -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES=arm64 \
  -DMEGA_SDK_DIR="$HOME/src/mega-sdk" -DVCPKG_ROOT="$HOME/src/vcpkg" \
  -DVCPKG_TARGET_TRIPLET=arm64-osx
cmake --build build/mega-account --parallel
```

Linux x64 (Ubuntu 24.04; install CMake, a C++20 compiler, pkg-config, autoconf, libtool and the usual OpenSSL/zlib development packages):

```bash
cmake -S Native/MegaAccount -B build/mega-account -DCMAKE_BUILD_TYPE=Release \
  -DMEGA_SDK_DIR="$HOME/src/mega-sdk" -DVCPKG_ROOT="$HOME/src/vcpkg" \
  -DVCPKG_TARGET_TRIPLET=x64-linux
cmake --build build/mega-account --parallel
```

The macOS and Linux builds use the SDK's OpenSSL curl backend and the `posix` network source. `LimitSdkReceive.cmake` selects `src/win32/net.cpp` on Windows and `src/posix/net.cpp` elsewhere, applies the same low-share pacing fix to a generated copy, and marks the original source header-only so the SDK builds only one receiver.

Copy the platform library into `Native/MegaAccount/prebuilt/<rid>/` before publishing the desktop app. `MegaDownloader.UI.csproj` copies the matching folder into the output when it exists. The GitHub `native-account` workflow builds both targets on their native runners and uploads the staged folders as artifacts.

## Try it locally

Run the application with the platform library beside it and sign in with MEGA account credentials. The pinned SDK constructor ignores its App Key parameter, so `MEGADOWNLOADER_MEGA_APP_KEY` is optional for this build.

- **MEGA account** page: add accounts, assign aliases, choose a default, refresh plan/storage/transfer details, sign out, remove. Remembered sessions restore at launch.
- **Add links**: choose Anonymous or a signed-in account as the download identity for direct public file links. The queue shows the identity per task and its context menu can reassign pending tasks.
- **MEGA cloud drive** page: browse the drive or incoming shares, search/type-filter, multi-select, preview the local folders and files, and queue the selection.
- Favorite and bind a cloud folder to a local directory; **Check for new files** compares the current cloud tree with the delivery history and offers new/changed files only.
- **Cloud directory subscriptions** page: edit the destination, extension rules, daily check time and review mode; check selected/all folders and queue the pending files. The scheduler checks due subscriptions every 30 seconds while the app runs.
- Private files keep partial progress in `.mega.part`, resume from the saved range and verify the whole file's MEGA MetaMAC before delivery.
- A public-file transfer can restart with the next eligible signed-in account when quota runs out. A private task stays with its original account and waits for its quota deadline.

The application reports the quota figures MEGA returns.
