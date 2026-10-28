# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game and its controls;
this file covers how the code is put together and the rules that keep it working.

## What this is

A turn-based, first-person dungeon role-playing game in the style of Sir-Tech's 1981 Wizardry: Proving Grounds of
the Mad Overlord, in C# on .NET 10 and Avalonia 12. One self-contained executable per platform (Windows, macOS and
Linux, x64 and ARM). The maze, monsters and castle are drawn as vector pictures in code.

## Commands

```bash
dotnet build AVAWizardry.slnx                     # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAWizardry.slnx                      # every test runs headless: no window opens
dotnet run --project src/Wizardry.Desktop         # play it
WIZ_SCREENSHOT_DIR=$PWD/docs/screenshots dotnet test tests/Wizardry.Desktop.Tests   # refresh the README's pictures
python3 tools/retro_audio.py <effects-dir> <music-dir>   # synthesise the retro pack (stdlib Python; ffmpeg for Ogg)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types, implicit usings and central
package versions (`Directory.Packages.props`) for every project: a warning fails the build, so keep it at zero.

## Layout

- `src/Wizardry.Core/`: the game, with no Avalonia in it.
  - `Screens/Game.cs` holds the world, the `Rng` and a stack of states. Hosts call `Press(key)` and `Submit(text)`
    and draw `Current`, a `Screen` of text lines, menu options and a picture pane.
  - `Screens/*States.cs`: the castle, shops, training, maze, combat and character-sheet screens.
  - `Combat/` (battles, magic, monster groups), `Content/` (the item, monster and spell tables), `Maze/` (the ten
    levels and `MazeBuilder`), `Model/` (characters, progression, the `World`) and `Rules/` (`Rng`, `Dice`, rules).
- `src/Wizardry.Desktop/`: the Avalonia app, MVVM with CommunityToolkit.Mvvm.
  - `ViewModels/` and `Views/` for the title, game, help and settings screens.
  - `Controls/` draws the maze, the automap and the pictures. `Input/KeyMap.cs` turns keys into the game's key
    names (`UP`, `ENTER`, letters, digits).
  - `Services/AppServices.cs` wires saves, settings and audio; `CreateIsolated(root)` is the test version.
  - `Assets/Help/*.md` are the in-game help pages. `soundpacks/` and `ambience/` ship as loose files.
- `src/Wizardry.Infrastructure/`: `SaveStore`, `GameSettings`, `AtomicFile` and `UserDataPaths`.
- `src/Wizardry.Audio/`: the OpenAL Soft audio engine, sound packs (`soundpack.json`) and `SoundDirector`.
- `tests/Wizardry.Core.Tests/`: xUnit v3 tests of rules, combat, mazes, saves and screens. `Bot.cs` plays the game
  through its screens and is used to show it can be won.
- `tests/Wizardry.Desktop.Tests/`: `Avalonia.Headless.XUnit` UI tests that press keys on the real window.

## Rules that keep it working

- **One random source.** All game randomness comes from the game's seeded `Rng` (SplitMix64, `Game.Rng`). Its
  state is saved in the `World`, so a loaded game continues exactly and tests can replay from a seed. Never use
  `Random.Shared` in `Wizardry.Core`; the desktop app only uses it to pick a new game's seed.
- **Core first, drawing second.** Gameplay belongs in `Wizardry.Core`, where tests drive it by pressing the keys a
  `Screen` offers. The desktop app only draws screens and maps keys.
- **Saves and settings.** Saves live in the local app-data folder (`AVAWizardry/saves`); settings and the player's
  own sound packs live in the roaming app-data folder (`AVAWizardry`). Tests use `AppServices.CreateIsolated` with
  a temporary folder and a silent `NullAudioEngine`, so they never touch the player's files.
- **Audio.** OpenAL Soft through Silk.NET, decoding Ogg with NVorbis and MP3 with NLayer.
  `Directory.Build.targets` swaps in the official Windows `soft_oal.dll` from `src/Wizardry.Audio/native/`, which
  needs no Visual C++ runtime. `OpenAlConfig` writes a larger mixing period to `ALSOFT_CONF` (unless the player
  set it) so the sound doesn't break up in a virtual machine.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Add a dated line to `src/Wizardry.Desktop/Assets/Help/07-whats-new.md` for user-facing changes, and update
  `README.md` and the other help pages to match.
- New sound files need their licence recorded next to them (`CREDITS`, `LICENSE.txt`) and in
  `THIRD_PARTY_NOTICES.md`.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request, using the SDK from `global.json`. It
then publishes self-contained single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64,
kept as workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag
makes a proper release.
