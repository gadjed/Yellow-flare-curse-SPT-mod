# Yellow Flare Curse

**SPT 4.0.13 Compatible**

Firing a successful **RSP-30 Yellow** flare starts a once-per-raid curse: scavs and PMC bots already on the map aggro you (and your group), then after **10 minutes** an airdrop lands at the nearest airdrop point with high-value loot.

Developed and tested against **SPT 4.0.13**.

[Latest release](https://github.com/gadjed/Yellow-flare-curse-SPT-mod/releases/latest) · [License: MIT](LICENSE)

## Features

- Trigger: vanilla **RSP-30 Yellow** only (not Special Yellow / red airdrop flares)
- **One event per raid**
- Curse snapshot: existing scav + PMC bots become hostile to you and your group
- Bosses / followers / sectants / rogues are **not** cursed
- Newly spawned bots (e.g. from Scav Population) stay uncursed
- After the delay, airdrop at the **nearest `AirdropPoint`** to the flare
- High-value crate loot (bitcoins, LEDX, military electronics, money, top ammo) via server config
- On-screen countdown (toggle in F12)
- Maps without airdrop points: event does not start

## Install

1. Download `YellowFlareCurse-*.zip` from [Releases](https://github.com/gadjed/Yellow-flare-curse-SPT-mod/releases)
2. Extract into your **SPT game root** (folder with `SPT.Server.exe` / `user/`)
3. Restart the SPT server **and** the game client

Paths inside the zip:

```text
user/mods/YellowFlareCurse/YellowFlareCurse.dll
user/mods/YellowFlareCurse/config.json
BepInEx/plugins/YellowFlareCurse.Client.dll
```

## Config (server)

Edit `user/mods/YellowFlareCurse/config.json`:

| Key | Description |
|-----|-------------|
| `Enabled` | Master toggle for server loot mapping |
| `AirdropDelaySeconds` | Hint only — live delay is the client F12 value |
| `CurseContainerId` | Synthetic container id used for custom loot (must match client constant) |
| `ForcedLoot` | Template id → `{ "Min", "Max" }` stack ranges packed into the curse crate |

## Config (client / F12)

| Setting | Default | Description |
|---------|---------|-------------|
| Enabled | true | Master toggle |
| AirdropDelaySeconds | 600 | Delay before the airdrop |
| ShowCountdown | true | On-screen MM:SS countdown |
| IncludePlayerGroup | true | Also mark teammates as enemies for cursed bots |
| Debug | false | Verbose logging |

## Compatibility

- Designed to work alongside **Scav Population** (new waves are not auto-cursed)
- **SAIN** optional — cursed bots fight more aggressively if SAIN is installed
- Requires maps that have vanilla airdrop points

## Build from source

Requires **.NET 9** SDK. Client needs hollowed refs under `Client/References/` (see that folder's README).

```bash
dotnet build YellowFlareCurse.sln -c Release
```

Output is copied to `Build/SPT/`.

## License

MIT — see [LICENSE](LICENSE).
