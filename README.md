# DeskHelper (WoW fishing bot)

Watches the fishing bobber on screen and reels in fish. Windows only, .NET 11.

## Run

```powershell
dotnet run --project src/FishingBot/FishingBot.csproj
```

## Build an exe

```powershell
dotnet publish ./src/FishingBot/FishingBot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
./publish/DeskHelper.exe
```

## Settings (environment variables)

| Variable | Default | Effect |
|---|---|---|
| `FISHINGBOT_DEBUG` | `false` | Verbose logging, saves success screenshots |
| `FISHINGBOT_ALT_TAB` | `false` | Alt-tab back to the previous window after focusing WoW |
| `FISHINGBOT_USE_AUDIO` | `false` | Also require a bite sound (otherwise bobber movement only) |
| `FISHINGBOT_CURSOR_CHECK` | `true` | Click only if the cursor changes over the bobber |

Example: `$env:FISHINGBOT_DEBUG = "true"; ./publish/DeskHelper.exe`
