# MEGA account module (Windows x64)

This native DLL connects the WinForms app to the official [MEGA SDK v10.21.1](https://github.com/meganz/sdk/releases/tag/v10.21.1). A prebuilt copy lives in `prebuilt/x64/`, so a normal build of the application ships the account feature without any extra tooling; the application can also build and download anonymously when those files are absent.

## Prebuilt copy

`prebuilt/x64/` contains `MegaAccountBridge.dll`, its three VC++ runtime DLLs (`msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`), and `MEGA-SDK-LICENSE.txt`. MSBuild compresses and embeds the DLLs. Both release downloads use the same EXE, which unpacks the native files to `%LOCALAPPDATA%\MegaDownloader\native\<version>\` on first use. Ordinary releases reuse the prebuilt files. Rebuild when the bridge source, SDK pin, or native dependencies or build options change.

## Build

Use Visual Studio 2022 with desktop C++, CMake, and a bootstrapped vcpkg repository with the SDK's `builtin-baseline` available (`ef7dbf94b9198bc58f45951adcf1f041fcbc5ea0`). Obtain the SDK at tag `v10.21.1` separately from this repository. From the application root, substitute your source and vcpkg paths:

```powershell
cmake -S Native/MegaAccount -B bin/MegaAccountNative -G 'Visual Studio 17 2022' -A x64 -DMEGA_SDK_DIR=C:/src/mega-sdk -DVCPKG_ROOT=C:/src/vcpkg -DUSE_OPENSSL=OFF
cmake --build bin/MegaAccountNative --config Release --target MegaAccountBridge
```

`USE_OPENSSL=OFF` is the supported configuration on Windows: the SDK's curl uses the system Schannel backend, which avoids an OpenSSL dependency entirely. If vcpkg fails while downloading a source archive behind a proxy, place the archive in the vcpkg `downloads` folder under the name its portfile expects and re-run the configure step.

Copy the new DLL over `prebuilt/x64/MegaAccountBridge.dll`. To test a rebuilt bridge with the prebuilt runtime DLLs, pass its path to MSBuild:

```powershell
dotnet msbuild MegaDownloader.vbproj /p:Configuration=Release /p:Platform=x86 "/p:MegaAccountBridgePath=$PWD\bin\MegaAccountNative\Release\MegaAccountBridge.dll"
```

To use a different set of DLLs, pass `/p:MegaAccountPrebuiltDir=...` and put all four DLLs in that directory.

The managed project uses AnyCPU with Prefer 32-bit disabled, including in the solution configuration named `x86`. The native module currently supports the x64 process. When distributing it, keep the [SDK license](https://github.com/meganz/sdk/blob/v10.21.1/LICENSE) with the binaries.

## Try it locally

Run the x64 application with the `MegaAccountBridge.dll` built from the same version of this repository beside it and enter account credentials in the sign-in dialog. The pinned SDK constructor ignores its App Key parameter, so `MEGADOWNLOADER_MEGA_APP_KEY` is optional for this build.

- Open **Sign in to MEGA** on the right of the menu bar. Add accounts, assign aliases, and set a default for new tasks. The account window shows the plan, storage usage, and transfer values returned by MEGA. The status strip summarizes the default account. A free account may have usage data shared by IP and no fixed transfer allowance.
- In **Add links**, use **Download identity** to select anonymous download or a specific account for a public single-file link. Use anonymous for folders and ELC. The queue shows the identity per task. Use its **Change download identity** menu to reassign selected pending public file tasks or packages.
- Open **Cloud drive** next to **Add links** and choose **Cloud drive** or **Shared with me**. Use Shift to check a range, or use **Batch select** for the current results. The download preview shows the local subfolders and files already in the queue.
- Favorite a cloud folder, bind it to a local directory, then use **Check for new files** to preview new and updated downloads. The app compares completed cloud deliveries with files still present at the bound destination and uses collision-safe filenames when writing updates.
- A public folder subscription downloads each file through the account selected for that subscription. `md_inspect` and `md_download_with_limit` also accept a canonical `https://mega.nz/folder/<share>#<key>/file/<node>` link: a temporary folder client inherits the account proxy, resolves and authorizes the node, and the signed-in account's client performs the transfer. Folder contents are always checked through the public link, so a signed-out account only pauses the tasks that wait for it. This path is compiled against the pinned SDK but has not been exercised with a live signed-in transfer on this machine.
- A public-file transfer can restart with the next eligible signed-in account when quota runs out. The updated bridge reports the retry delay through `md_download_with_quota`. The queue retries when the reported deadline passes. A private task stays with its original account and shows a countdown when the SDK reports a delay. Right-click a private task to refresh quota or requeue it.
- Private files keep their partial progress through pause and restart. The SDK continues from the saved range and the app verifies the entire file's MEGA MetaMAC before delivery. The updated bridge includes `md_list_shares`, `md_inspect_node_for_resume`, and `md_stream_node`.
- **Keep me signed in on this device** saves that account's SDK session with Windows current-user DPAPI. Sign-out clears the remembered session and removal deletes the account entry.

The application reports the quota figures MEGA returns. A successful public download alone cannot establish how MEGA attributes the transferred bytes.
