# DeskHelper (WoW fishing bot)

Watches the fishing bobber on screen and reels in fish for you. Windows only.

- [For players](#for-players)
- [For developers](#for-developers)

---

# For players

## Download

1. Go to the [Releases page](https://github.com/ThomasWillumsen/WowFishingBot/releases) and download `DeskHelper.exe` from the latest release.
2. No installation is needed. You don't need .NET installed.

## Before you start

- Put your **Fishing** spell on action bar key **`0`**. The bot presses `0` to cast.
- Keep the WoW window visible on the screen. The bot reads the screen, so other windows can't cover the game.
- **Zoom the camera all the way in** (first person) so your character is not visible. Your character's colors and animations would otherwise get in the way.
- **Aim the camera so the bobber lands just above the center of the screen.** The bot only looks at a small rectangle there: it's centered horizontally and sits directly above the middle of the game window. If the bobber lands outside that rectangle, the bot can't find it.
- **Avoid red things in that area.** The bot finds the bobber by looking for its **red feather**, so stand where the water near the center of the screen has no other red (red flowers, red banners, red buildings, red lava and so on). 

## Use it

1. Start WoW and stand at your fishing spot, facing the water as described above.
2. Start `DeskHelper.exe`. A console window opens.
3. The bot casts, finds the bobber, watches it for movement, and reels in fish by right-clicking the bobber when a fish bites.
4. Press `Ctrl+C` in the console window to stop.

Don't move the camera while the bot is running. If the bobber can't be found 3 casts in a row, the bot stops and saves screenshots to a `logs` folder next to where you started it. Use them to see what the bot saw, then adjust the camera.

> Automating gameplay may be against the game's Terms of Service. Use at your own risk.

## Optional settings

Set these as environment variables before starting the exe.

| Variable | Default | Effect |
|---|---|---|
| `FISHINGBOT_DEBUG` | `false` | Verbose logging, saves success screenshots, and writes each catch (with the bobber movement %) to `logs/catches.log` |
| `FISHINGBOT_ALT_TAB` | `false` | Alt-tab back to the previous window after focusing WoW |
| `FISHINGBOT_USE_AUDIO` | `false` | Also require a bite sound (otherwise bobber movement only) |
| `FISHINGBOT_CURSOR_CHECK` | `true` | Click only if the cursor changes over the bobber |

Example (PowerShell): `$env:FISHINGBOT_DEBUG = "true"; ./DeskHelper.exe`

---

# For developers

Requires the .NET 11 SDK.

## Run

```powershell
dotnet run --project src/FishingBot/FishingBot.csproj
```

## Build an exe

```powershell
dotnet publish ./src/FishingBot/FishingBot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
./publish/DeskHelper.exe
```

## Releases

Every push to `master` runs [the release workflow](.github/workflows/release.yml), which publishes a single-file `DeskHelper.exe` and attaches it to a new GitHub release tagged `v1.0.<run number>`.

## How it works

- `Vision/BobberLocator.cs` captures a rectangle above the center of the game window (sized relative to the client height) and looks for the reddish bobber feather.
- `Vision/BiteWatcher.cs` watches the area around the bobber and reports how many pixels changed; a spike means a bite. With `FISHINGBOT_USE_AUDIO` a matching sound spike is required too.
- `Worker.cs` runs the cast, watch, reel-in loop, with small random delays to behave like a person.
