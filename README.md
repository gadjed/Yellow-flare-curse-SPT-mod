# Yellow Flare Curse

**SPT 4.0.13 / 4.1.x** · **v1.2.0**

Firing a successful **RSP-30 Yellow** flare starts a once-per-raid curse: scavs and PMC bots aggro you (and your group), then after a delay an airdrop lands near the flare with **forced high-value loot**.

[Latest release](https://github.com/gadjed/Yellow-flare-curse-SPT-mod/releases/latest) · [License: MIT](LICENSE)

## Features

- Trigger: vanilla **RSP-30 Yellow** handheld  
  - Item in inventory: `624c0b3340357b5f566e8766`  
  - Ammo id reported on success: `624c09e49b98e019a3315b66` (`patron_rsp_yellow`)
- **One event per raid**
- Curse: eligible scav/PMC bots get `AddEnemy` + last-known position (`ReportAboutEnemy` / `CalcGoalForBot`)
- **SAIN-aware** (optional): sets known place so bots actually hunt under SAIN (`EnemyKnown`)
- **QuestingBots-aware** (optional): calls `StopQuesting` on cursed bots so PMC/PScavs leave quest paths
- Bosses / followers / sectants / rogues are **not** cursed
- Curse **refreshes every 5s** while the event is active (covers new spawns)
- After the delay, airdrop at the **nearest `AirdropPoint`** to the flare
- Forced high-value crate (bitcoins, LEDX, GPUs, intel, cases, keycards, top ammo, money) — **not** random Common/«общей поддержки»
- Bottom-right **start banner + countdown**; overlay **hides ~5s after airdrop**
- Maps without airdrop points: event does not start
- Logging: BepInEx / Unity / SPT server console + dedicated log files

## Install

1. Download `YellowFlareCurse-1.2.0.zip` from [Releases](https://github.com/gadjed/Yellow-flare-curse-SPT-mod/releases)
2. Extract into your **SPT game root** (folder with `SPT.Server.exe` / `user/`)
3. Restart the SPT **server** and the **game client**

```text
user/mods/YellowFlareCurse/YellowFlareCurse.dll
user/mods/YellowFlareCurse/config.json
BepInEx/plugins/YellowFlareCurse.Client.dll
```

## Changelog (1.2.0)

- Fix yellow flare detection (ammo tpl vs weapon tpl)
- Stronger bot hunt (report position + SAIN known place + refresh)
- Forced loot profile via Supply-style mapping (not mixed/Common)
- Richer default ForcedLoot table
- Multi-sink logging (file / console / server)
- Bottom-right overlay; auto-hide after airdrop

## Logging

| Where | Path / sink |
|-------|-------------|
| Client file | `BepInEx/plugins/YellowFlareCurse/logs/yellowflarecurse-*.log` |
| Client console | BepInEx `LogOutput.log` + Unity player log |
| Server console | SPT server window / `user/logs/spt/` |
| Server file | `user/mods/YellowFlareCurse/logs/yellowflarecurse-server-*.log` |

F12 **Debug** logs every flare success with template id.

## Config (server)

Edit `user/mods/YellowFlareCurse/config.json`:

| Key | Description |
|-----|-------------|
| `Enabled` | Master toggle for server loot mapping |
| `AirdropDelaySeconds` | Hint only — live delay is the client F12 value |
| `CurseContainerId` | Synthetic container id (must match client constant) |
| `ForcedLoot` | Template id → `{ "Min", "Max" }` stacks packed into the curse crate |

## Config (client / F12)

| Setting | Default | Description |
|---------|---------|-------------|
| Enabled | true | Master toggle |
| AirdropDelaySeconds | 600 | Delay before the airdrop |
| ShowCountdown | true | Banner + countdown (bottom-right) |
| IncludePlayerGroup | true | Also mark teammates as enemies for cursed bots |
| Debug | false | Verbose logging |

## Compatibility

- **Scav Population** — new waves get cursed on the refresh timer
- **SAIN** optional — recommended for aggressive hunting
- **Fika** — host must fire the flare (`IsYourPlayer`); client `InitAirdrop` is a no-op
- Requires maps with vanilla airdrop points

## Build from source

Requires **.NET 9/10** SDK. Client needs hollowed refs under `Client/References/` (see that folder's README).

```bash
dotnet build YellowFlareCurse.sln -c Release
```

Output is copied to `Build/SPT/`.

## License

MIT — see [LICENSE](LICENSE).
