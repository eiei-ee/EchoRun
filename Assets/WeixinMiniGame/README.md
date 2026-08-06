# WeChat MiniGame v0

This directory contains the WeChat-only presentation layer and the saved
Tuanjie Build Profile. The gameplay and AI implementations remain in
`Assets/Scripts` and compile from the shared `TempleRun.Runtime` assembly.

## Required official platform package

Tuanjie 2022.3.62t8 requires the official WeChat conversion package
`com.qq.weixin.minigame` (`WX-WASM-SDK-V2`). Install it from the WeChat entry
in Build Profiles. The build command intentionally fails before conversion if
`Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset` is absent; it never
downloads or installs dependencies automatically.

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

For WeChat DevTools, pass the token at runtime through
`WECHAT_DEVTOOLS_CLI_TOKEN`; do not commit it to this repository.

## Package notes

- The bundled OFL-licensed Noto Sans CJK font is subset to the characters used
  by the project; `OFL.txt` remains beside it.
- The verified raw Weixin player data is 3,579,884 bytes (below 4 MiB). Final
  first-package sizing still requires the official converter above.
- The runtime layer performs no network requests and adds no AI dependency.
