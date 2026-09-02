# Third-Party Notices

This file records third-party material that is included in the repository.
The MIT license at the repository root applies to the project's source code,
not to the assets listed below. Each listed asset remains subject to its own
license or terms.

| Asset | Path | Source | License / permission |
| --- | --- | --- | --- |
| EchoRun Sans SC Regular (project subset) | `Assets/Resources/Fonts/EchoRunSansSC-Regular.otf` | [Noto Sans CJK 2.004 official release](https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004), `Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf` | Derived from Noto Sans CJK SC Regular 2.004 and renamed for this project. SIL Open Font License 1.1; the license text is included at `Assets/Resources/Fonts/OFL.txt`. |
| Tense Future Loop | `Assets/Resources/Audio/bgm_transit.ogg` | [OpenGameArt](https://opengameart.org/content/tense-future-loop) | CC0. |
| Impact Sounds | `Assets/Resources/Audio/footstep_01.ogg`, `Assets/Resources/Audio/footstep_02.ogg`, `Assets/Resources/Audio/collision.ogg` | [Kenney](https://kenney.nl/assets/impact-sounds) | CC0. |
| RPG Audio | `Assets/Resources/Audio/coin.ogg` | [Kenney](https://kenney.nl/assets/rpg-audio) | CC0. |
| Interface Sounds | `Assets/Resources/Audio/ui_click.ogg`, `Assets/Resources/Audio/ui_confirm.ogg`, `Assets/Resources/Audio/ui_error.ogg` | [Kenney](https://kenney.nl/assets/interface-sounds) | CC0. |
| Exo Gray humanoid character | `Assets/Models/Mixamo/ExoGray` | [Adobe Mixamo](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Adobe permits royalty-free use of Mixamo characters and animations in personal, commercial, and non-profit video-game projects. The character remains subject to Adobe's terms and is not covered by this repository's MIT license. |
| Standard Assets humanoid animations | `Assets/Animations/HumanMotion` | [Unity Standard Assets Characters](https://github.com/Unity-Technologies/Standard-Assets-Characters) | Unity Companion License; the source notice and license text are included at `Assets/ThirdParty/UnityStandardAssets`. |
| WeChat Mini Game Tuanjie adapter SDK | Git package `com.qq.weixin.minigame` and the tracked `Assets/WX-WASM-SDK-V2` compatibility mirror | [wechat-miniprogram/minigame-tuanjie-transform-sdk v0.1.32](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk/tree/v0.1.32) | MIT License; the upstream copyright and license text are included at `Assets/ThirdParty/WeChatMiniGameSDK`. |

## Assets created for this project

Project-authored assets (for example, scripts, prefabs, materials, scene data,
and `Assets/Resources/Art/EchoSky.png`) are intended to be covered by the
repository's MIT license only where the copyright holder is entitled to grant
that license. If an asset is later found to incorporate third-party work, add
its attribution and terms to this file before redistributing it.

## EchoRun Sans SC provenance

- Upstream source: official `notofonts/noto-cjk` tag `Sans2.004`, static
  Simplified Chinese Regular OTF.
- Upstream SHA-256:
  `2C76254F6FC379FDDFCE0A7E84FB5385BB135D3E399294F6EEB6680D0365B74B`.
- Bundled subset SHA-256:
  `CCCAD320E18B33279AB48E88517D6312A9541B5DE06E65E3C777B67BA09724FA`.
- The subset retains the upstream Adobe copyright and Google trademark
  acknowledgement, but its user-facing family and PostScript names are
  `EchoRun Sans SC` and `EchoRunSansSC-Regular` so it cannot be mistaken for
  an unmodified upstream font.
- Rebuild with FontTools 4.55.0:
  `python Tools/Fonts/build_echorun_font.py <official-2.004-regular.otf>`.
  The pinned input hash, Unicode set, naming, weight and static-font checks are
  enforced by the script.

## Release checklist

Before publishing a Release, verify that every bundled asset has an entry
above or is original work whose rights you control. Remove or replace any
asset with missing or incompatible redistribution terms. Package-manager
dependencies remain subject to the licenses distributed with their packages.

## Historical source notice

Revisions before commit `ffb82b5` may contain an unused
`Assets/Fonts/arial.ttf` file whose original source and redistribution rights
were not recorded. The file is not covered by this repository's MIT License
and was removed from the current default branch. Do not redistribute an older
source snapshot without removing that file or independently establishing its
license. GitHub's read-only `refs/pull/1/head` still exposes the pre-rewrite PR
commit `1f03857`; ordinary Git pushes cannot delete this special reference.
Removing that backend reference requires GitHub Support. See the current
[release-candidate audit](docs/releases/v0.2.0-alpha.1-audit.md).
