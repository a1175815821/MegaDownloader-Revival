# MEGA account module (Windows x64)

This optional native DLL connects the WinForms app to the official [MEGA SDK v10.21.1](https://github.com/meganz/sdk/releases/tag/v10.21.1). The application still builds and downloads anonymously without it. The account dialog reports a missing module or App Key instead of attempting an anonymous login or download.

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

Set `MEGADOWNLOADER_MEGA_APP_KEY` to your application's MEGA App Key in the process environment, then launch the built app from the same terminal. The App Key identifies the application; it is not the user's password or session. Enter account credentials only in the in-app dialog.

- Use **Sign in to MEGA** at the right of the top menu. The bottom status shows the login state or transfer balance when MEGA reports it. A free account may not have a numeric allowance to display.
- Use **Add links** → **Use MEGA account (public files only)** for a direct public single-file link. The queued task stays assigned to that email. Folders, ELC, and anonymous downloads continue to use the existing path.
- If you select **Keep me signed in on this device**, restart and verify the account is restored. Only the SDK session and email are stored with Windows current-user DPAPI. Sign out from the account dialog to clear the saved login.
- Test downloading a small file not in this account's cloud drive, correct output bytes, cancellation, and sign-out. Account transfers use the SDK, not the original anonymous `.part` file format. Pausing or stopping an account transfer starts it again from the beginning on resume in this version. Anonymous resumption is unchanged.

Signed-in transfer quota attribution, online login and session restoration need real-account testing; offline regressions do not verify how the service attributes quota.
