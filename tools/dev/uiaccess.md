# 输入组件的开发接口

本文面向维护 CLI/宿主桥接的开发者。普通用户安装、签名要求与部署边界见[可选组件指南](../../docs/dev/uiaccess.md)。只在约定的运行输入路径使用 helper；录制、只读校验与常规 GUI 不应依赖它。

## Private pipe

A cryptographically random pipe name and nonce are generated per run. The server uses `NamedPipeServerStreamAcl.Create` with an explicit protected DACL granting FullControl only to the current TokenUser SID (`WindowsIdentity.GetCurrent().User`), not token owner. `CurrentUserOnly` is deliberately avoided because its framework owner/elevation assumptions are inappropriate for the ordinary-editor/UIAccess boundary; split tokens may have a different owner SID. This establishes the intended SID authorization, but actual cross-integrity connectivity remains signed-installation acceptance. The helper must match the ShellExecute process PID, actual TokenUIAccess=1, current user and session. The helper independently checks server PID/current user/session and echoes the launch nonce. Arguments and flow paths are sent only after authentication. No job/output/log files are created.

Frames are little-endian int32 byte length followed by UTF-8 JSON, maximum 1 MiB. Types: hello(nonce), job(args, workingDirectory, environment), stdout(text), stderr(text), pause(paused), cancel, exit(exitCode). This is private C# protocol, not a Rust integration surface. Job accepts only capability commands, at most 256 args, a fully qualified existing working directory, and environment allowlist `KEYMOUSE_HUD`, `KEYMOUSE_OUTPUT_ENCODING`. The helper cannot run an arbitrary executable, nested bridge, editor, service or elevation command. The helper detaches from any console and uses managed pipe text writers; output is re-encoded by the ordinary bridge for its caller.

## Build tool

```powershell
./tools/dev/build-uiaccess.ps1 -OutputDirectory artifacts/uiaccess/my-new-build
```

The output directory must not exist. `-Runtime win-x64` is default; `win-arm64` is available but has not been validated here. The tool publishes a self-contained managed single-file WinExe with `/p:UIAccessFlavor=true`, manifest `asInvoker uiAccess=true`, renames the apphost to KeyMouse.Input.exe, strips build-only ONNX import libraries, and writes an inventory outside the install payload. It never signs, installs a certificate, installs the helper, or runs it.

Actual validated win-x64 payload:

- KeyMouse.Input.exe
- onnxruntime.dll
- onnxruntime_providers_shared.dll

All three belong in the protected Input directory. Before receiving a job, the helper restricts native DLL search to APPLICATION_DIR and SYSTEM32, excluding job CWD/PATH. Native libraries remain beside the image: `IncludeNativeLibrariesForSelfExtract=false` deliberately avoids writable TEMP extraction. Do not copy only the EXE. No runtimeconfig/deps JSON is required. OCR model data is not bundled by this tool; existing model discovery still applies, and deployments requiring OCR must separately consider model provenance.

Windows UIAccess deployment requires an appropriate Authenticode signature whose chain is trusted on the target machine, a protected trusted installation location under Program Files (default policy), and normal Windows image loading checks. An unsigned developer build in artifacts is not a working UIAccess installation. Sign after publishing; changing a signed image invalidates its signature. Each native DLL must also meet installer signature checks. Build success does not establish that Windows grants UIAccess.

UIAccess flavor sets `StartupHookSupport=false` (SDK maps to `System.StartupHookProvider.IsSupported=false`). This disables managed startup hooks; it is not evidence that all CLR profiler/native runtime environment injection is disabled. ShellExecute does not provide an explicit child environment block, and AppInfo's UIAccess environment filtering has not been verified. Pre-main profiler/environment hardening remains an unresolved production deployment blocker: do not treat signing and protected files alone as proof that user-controlled CLR environment cannot affect startup. No unsupported `EnableDiagnostics=false` claim is made.


## 回归入口

在 [UIAccess 测试](../../tests/KeyMouse.UIAccess.Tests)中验证参数转交与失败处理。缺少签名或专用测试桌面时，只能报告未签名拒绝与协议检查的结果，不能据此宣称已验证真实跨权限输入。
