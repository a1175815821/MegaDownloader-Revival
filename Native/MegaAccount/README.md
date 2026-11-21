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

Run the x64 application with `MegaAccountBridge.dll` beside it and enter account credentials in the sign-in dialog. The pinned SDK constructor ignores its App Key parameter, so `MEGADOWNLOADER_MEGA_APP_KEY` is optional for this build.

- Open **Sign in to MEGA** on the right of the menu bar. Add accounts, assign aliases, and set a default for new tasks. The account window shows the plan, storage usage, and transfer values returned by MEGA. The status strip summarizes the default account. A free account may have usage data shared by IP and no fixed transfer allowance.
- In **Add links**, use **Download identity** to select anonymous download or a specific account for a public single-file link. Use anonymous for folders and ELC. The queue shows the identity per task. Use its **Change download identity** menu to reassign selected pending tasks or packages.
- When an account reaches its transfer quota, the current task restarts with the next signed-in account that has no reported quota block. The queue shows the account used for the latest attempt. Changing the default keeps existing task assignments. If every account reaches its quota, refresh account details and retry the task when allowance is available.
- **Keep me signed in on this device** saves that account's SDK session with Windows current-user DPAPI. The app saves account names, aliases, and the default separately from the decision to remember each session. Sign-out clears the remembered session and removal deletes the account entry.
- To check the live flow, sign in with an authorized account, download a known small public file, compare the output bytes, stop a running transfer, then restart the app to check session restoration and sign out. Account transfers start over after a pause or a quota-driven account switch. Anonymous transfers use their existing partial files.

The application reports the quota figures MEGA returns. A successful public download alone cannot establish how MEGA attributes the transferred bytes.
