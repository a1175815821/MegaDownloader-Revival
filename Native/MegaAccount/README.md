# MEGA account module (Windows x64)

This optional native DLL connects the WinForms app to the official [MEGA SDK v10.21.1](https://github.com/meganz/sdk/releases/tag/v10.21.1). The application can also build and download anonymously without the DLL.

## Build

Use Visual Studio 2022 with desktop C++, CMake, and a bootstrapped vcpkg repository with the SDK's `builtin-baseline` available (`ef7dbf94b9198bc58f45951adcf1f041fcbc5ea0`). Obtain the SDK at tag `v10.21.1` separately from this repository. From the application root, substitute your source and vcpkg paths:

```powershell
cmake -S Native/MegaAccount -B bin/MegaAccountNative -G 'Visual Studio 17 2022' -A x64 -DMEGA_SDK_DIR=C:/src/mega-sdk -DVCPKG_ROOT=C:/src/vcpkg
cmake --build bin/MegaAccountNative --config Release --target MegaAccountBridge
```

Pass the resulting absolute DLL path as `MegaAccountBridgePath` when building the application. For example:

```powershell
dotnet msbuild MegaDownloader.vbproj /p:Configuration=Release /p:Platform=x86 "/p:MegaAccountBridgePath=$PWD\bin\MegaAccountNative\Release\MegaAccountBridge.dll"
```

The managed project uses AnyCPU with Prefer 32-bit disabled, including in the solution configuration named `x86`. The native module currently supports the x64 process. When distributing it, include the [SDK license](https://github.com/meganz/sdk/blob/v10.21.1/LICENSE) and dependency notices from vcpkg. The DLL uses the Microsoft Visual C++ runtime.

## Try it locally

Run the x64 application with the `MegaAccountBridge.dll` built from the same version of this repository beside it and enter account credentials in the sign-in dialog. The pinned SDK constructor ignores its App Key parameter, so `MEGADOWNLOADER_MEGA_APP_KEY` is optional for this build.

- Open **Sign in to MEGA** on the right of the menu bar. Add accounts, assign aliases, and set a default for new tasks. The account window shows the plan, storage usage, and transfer values returned by MEGA. The status strip summarizes the default account. A free account may have usage data shared by IP and no fixed transfer allowance.
- In **Add links**, use **Download identity** to select anonymous download or a specific account for a public single-file link. Use anonymous for folders and ELC. The queue shows the identity per task. Use its **Change download identity** menu to reassign selected pending public file tasks or packages.
- Open **Cloud drive** next to **Add links** and choose **Cloud drive** or **Shared with me**. Use Shift to check a range, or use **Batch select** for the current results. The download preview shows the local subfolders and files already in the queue.
- Favorite a cloud folder, bind it to a local directory, then use **Check for new files** to preview new and updated downloads. The app compares completed cloud deliveries with files still present at the bound destination and uses collision-safe filenames when writing updates.
- A public-file transfer can restart with the next eligible signed-in account when quota runs out. The updated bridge reports the retry delay through `md_download_with_quota`. The queue retries when the reported deadline passes. A private task stays with its original account and shows a countdown when the SDK reports a delay. Right-click a private task to refresh quota or requeue it.
- Private files keep their partial progress through pause and restart. The SDK continues from the saved range and the app verifies the entire file's MEGA MetaMAC before delivery. The updated bridge includes `md_list_shares`, `md_inspect_node_for_resume`, and `md_stream_node`.
- **Keep me signed in on this device** saves that account's SDK session with Windows current-user DPAPI. Sign-out clears the remembered session and removal deletes the account entry.

The application reports the quota figures MEGA returns. A successful public download alone cannot establish how MEGA attributes the transferred bytes.
