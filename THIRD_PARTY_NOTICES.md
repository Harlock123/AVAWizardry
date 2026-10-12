# Third-party notices (AVAWizardry)

AVAWizardry itself is MIT-licensed (see `LICENSE`). The components below keep their own licences.

## Shipped with the application

| Component | Version | License | Use | Source |
|---|---|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Skia, Avalonia.HarfBuzz, Avalonia.Win32, Avalonia.X11, Avalonia.Native, Avalonia.FreeDesktop(.AtSpi), Avalonia.Remote.Protocol | 12.1.3 | MIT | UI framework | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Fonts.Inter (Inter typeface) | 12.1.3 | MIT (package); Inter font under SIL OFL 1.1 | UI font | https://github.com/AvaloniaUI/Avalonia, https://rsms.me/inter |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause style (ANGLE Project Authors) | Windows GPU backend | https://github.com/AvaloniaUI/angle |
| SkiaSharp (+ NativeAssets) | 3.119.4 | MIT (Skia: BSD-3-Clause) | Rendering | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp (+ NativeAssets) | 8.3.1.3 | MIT (HarfBuzz: MIT/"Old MIT") | Text shaping | https://github.com/mono/SkiaSharp |
| MicroCom.Runtime | 0.11.6 | MIT | Avalonia interop | https://github.com/kekekeks/MicroCom |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Linux desktop integration | https://github.com/tmds/Tmds.DBus |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM helpers | https://github.com/CommunityToolkit/dotnet |
| **VT323** font (`src/Wizardry.Desktop/Assets/Fonts/VT323-Regular.ttf`) | google/fonts commit 7085eb8 | **SIL Open Font License 1.1**; reserved font name none listed; © 2011 The VT323 Project Authors (Peter Hull) | Retro display font | https://github.com/google/fonts/tree/main/ofl/vt323 (license text: `Assets/Fonts/VT323-OFL.txt`, shipped alongside) |
| .NET runtime (self-contained builds) | 10.0.x | MIT | Runtime | https://github.com/dotnet/runtime |
| Silk.NET.OpenAL, Silk.NET.Core | 2.23.0 | MIT | OpenAL bindings | https://github.com/dotnet/Silk.NET |
| **OpenAL Soft** (native; via Silk.NET.OpenAL.Soft.Native 1.23.1; on Windows x64/x86 the unmodified official 1.23.1 binaries in `src/Wizardry.Audio/native/`) | 1.23.1 | **LGPL-2.0-or-later** | Audio mixing and output | https://github.com/kcat/openal-soft/tree/1.23.1 (licence: `src/Wizardry.Audio/native/COPYING-openal-soft.txt`) |
| NVorbis | 0.10.5 | MIT | Ogg Vorbis decoding | https://github.com/NVorbis/NVorbis |
| NLayer | 1.16.0 | MIT | MP3 decoding | https://github.com/naudio/NLayer |
| Microsoft.Extensions.DependencyModel / Microsoft.DotNet.PlatformAbstractions (via Silk.NET) | 9.0.9 / 3.1.6 | MIT | Native library loading | https://github.com/dotnet/runtime |

**OpenAL Soft (LGPL)**: it is not compiled into AVAWizardry. It is a separate shared library
(`soft_oal.dll` / `libopenal`) loaded at run time, which users may replace with their own build.
When distributing, ship its licence text (above) and point to its source (above, or offer it on
request).

Obligations: keep the copyright and license notices of the MIT/BSD components with redistributed
binaries (the NuGet packages contain them), and ship the OFL text with the VT323 font (it is
embedded next to it). The OFL allows bundling with software, including commercial software, but the
font may not be sold on its own.

## Sound and music shipped with the application

| Asset | Licence | Credit |
|---|---|---|
| `soundpacks/avawiz-retro` (sound effects) | CC0 | Synthesised for AVAWizardry by `tools/retro_audio.py` (adapted from the author's AVAGard) |
| `soundpacks/avawiz-music`: "The Proving Grounds", "Graph Paper" | CC0 | Composed and synthesised by `tools/retro_audio.py` ("Graph Paper" first made for AVAGard) |
| `soundpacks/avawiz-music`: "The Foe Before You" | CC0 | Made for AVABand by the same author |
| `soundpacks/avawiz-music`: 15 OpenGameArt tracks | CC0 | RandomMind, cynicmusic, JaggedStone, yd, HaelDB, TinyWorlds, Spring Spring, Paul Wortmann, Eponasoft, The Oracle, Juhani Junkala (see the pack's `CREDITS.txt`) |
| `ambience/` (3 dungeon loops) | CC0 | JaggedStone, gmason, bretbernhoft via OpenGameArt (see `ambience/CREDITS.md`) |
| `soundpacks/angband-dubtrain` (optional effects pack) | **CC BY 4.0, attribution required** | **Sound effects by Dubtrain** (angband@dubtrain.com), from Angband 4.2 `lib/sounds`, https://github.com/angband/angband/tree/master/lib/sounds. Licence text: the pack's `LICENSE.txt` |

## Code reused from the author's other games

`src/Wizardry.Audio` (OpenAL engine, decoders, loudness levelling, sound-pack loader and VM
detection), the Windows OpenAL swap in `Directory.Build.targets`, the save and settings plumbing,
and the music and ambience above come from the author's own **AVABand** and **AVAGard**. AVABand as
a whole is GPL-2.0 because it derives from Angband; its audio code is the author's original work,
not Angband-derived, so the author relicenses it here under AVAWizardry's MIT License.

## Build and test only (not shipped)

| Component | Version | License |
|---|---|---|
| Avalonia.BuildServices (build telemetry; opt out with `AVALONIA_TELEMETRY_OPTOUT=1`) | 11.3.2 | MIT |
| xunit.v3, xunit.runner.visualstudio | 3.2.2 / 3.1.5 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT |
| Avalonia.Headless, Avalonia.Headless.XUnit | 12.1.3 | MIT |

## Wizardry itself

*Wizardry: Proving Grounds of the Mad Overlord* © 1981 Sir-Tech Software (Andrew Greenberg and
Robert Woodhead); the Wizardry name is a trademark of its current owner. AVAWizardry is an
independent game in the same style, written from the published rules of the original (races,
classes and their requirements, alignment, the experience table, spell levels, the front rank,
chests and traps, the temple, aging, class changes). Its maze, story, names, spell names, monsters,
items, text, pictures, music and code are its own; no original program code, data, artwork, maps or
manual text is included. It is not affiliated with or endorsed by any rights holder. The name and
title appear in a few strings and can be replaced.
