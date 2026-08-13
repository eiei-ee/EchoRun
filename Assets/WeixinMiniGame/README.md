# WeChat MiniGame v0

This directory contains the WeChat-only presentation layer and the saved
Tuanjie Build Profile. The gameplay and AI implementations remain in
`Assets/Scripts` and compile from the shared `TempleRun.Runtime` assembly.

## Required official platform package

Tuanjie 2022.3.62t8 requires the official WeChat conversion package
`com.qq.weixin.minigame` (`WX-WASM-SDK-V2`). This project pins the version
required by the installed Tuanjie module (`0.1.32`) from WeChat's official
GitHub repository. The build also accepts the legacy SDK layout under
`Assets/WX-WASM-SDK-V2`.

## Build

```powershell
& 'D:\unity\tuanjie\2022.3.62t8\Editor\Tuanjie.exe' `
  -batchmode -quit `
  -projectPath 'C:\Users\zzz\Desktop\TempleRun' `
  -buildTarget WeixinMiniGame `
  -minigamesubplatform weixin `
  -executeMethod BuildConfig.BuildWeixinMiniGameV0
```

The converted output path is `Builds/WeixinMiniGameV0-Profile`.

Set a valid Mini Game AppID only for the build process when IDE validation is
required:

```powershell
$env:WECHAT_MINIGAME_APPID = 'wx...'
```

For WeChat DevTools, pass the token at runtime through
`WECHAT_DEVTOOLS_CLI_TOKEN`; do not commit it to this repository.

## Package notes

- The bundled OFL-licensed EchoRun Sans SC font is a project-named subset of
  Noto Sans CJK SC 2.004. `OFL.txt` remains beside it; the pinned source hash,
  Unicode set and reproducible build script are recorded in
  `THIRD_PARTY_NOTICES.md` and `Tools/Fonts/`.
- The converted main package is 982,708 bytes (0.937 MiB), below the 4 MiB
  target. `wasmcode` is a separate 5,340,536-byte subpackage; 6,658,702 bytes
  of debug symbols are excluded by `project.config.json`.
- The runtime layer performs no network requests and adds no AI dependency.
