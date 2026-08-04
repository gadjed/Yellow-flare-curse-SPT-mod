# Yellow Flare Curse

**SPT 4.0.13** · **v1.4.5**

Firing a successful **RSP-30 Yellow** flare starts a once-per-raid curse: scavs are pulled near you, stop fighting each other, hunt you (and your group), optional **Tagilla** / **cultists** spawn nearby, then after a delay an airdrop lands near the flare with **forced high-value loot** (when the map supports airdrops).

[Latest release](https://github.com/gadjed/Yellow-flare-curse-SPT-mod/releases/latest) · [License: MIT](LICENSE)

## Features

- Trigger: vanilla **RSP-30 Yellow** handheld  
  - Item in inventory: `624c0b3340357b5f566e8766`  
  - Ammo id reported on success: `624c09e49b98e019a3315b66` (`patron_rsp_yellow`)
- **One event per raid**
- **Tagilla** spawn on curse start (host/authority), placed near the player
- Optional **cultist squad** (priest + warriors) via F12 `SpawnCultists`
- **Teleport** eligible scav AI near the player on curse start (host/authority; NavMesh ring, never onto the player). **PMCs are not teleported**
- **AI alliance** during curse — scavs do not fight each other; they only hunt players
- Curse: scavs (+ Tagilla / cultists) get `AddEnemy` + last-known position (`ReportAboutEnemy` / `CalcGoalForBot`)
- **SAIN-aware** (optional): seen place + dangerous gunshot so bots hunt under SAIN
- **QuestingBots-aware** (optional): calls `StopQuesting` on cursed bots
- Other bosses / rogues are **not** cursed (unless spawned by this mod)
- Curse **refreshes every 5s** while the event is active (covers new spawns)
- After the delay, airdrop at the **nearest `AirdropPoint`** to the flare (if the map has any)
- Maps **without airdrop points**: curse + bosses still run; airdrop is skipped
- Forced high-value **SUPPLY** crate (bitcoins, LEDX, GPUs, military electronics, labs keycards, top ammo) — **not** Common/«общей поддержки» or weapon crates
- Bottom-right **start banner + countdown**; overlay **hides ~5s after airdrop** (or after the start banner when there is no airdrop)
- Logging: BepInEx / Unity / SPT server console + dedicated log files

## Install

1. Download `YellowFlareCurse-1.4.5.zip` from [Releases](https://github.com/gadjed/Yellow-flare-curse-SPT-mod/releases)
2. Extract into your **SPT game root** (folder with `EscapeFromTarkov.exe` / `SPT/`)
3. Restart the SPT **server** and the **game client**

```text
SPT/user/mods/YellowFlareCurse/YellowFlareCurse.dll
SPT/user/mods/YellowFlareCurse/config.json
BepInEx/plugins/YellowFlareCurse.Client.dll
```

## Changelog (1.4.5)

- Curse airdrop always builds SUPPLY crate + ForcedLoot (no Common/weapon junk crates)
- Teleport/curse limited to scavs; PMCs excluded
- Optional cultist squad spawn (`SpawnCultists`)

## Logging

| Where | Path / sink |
|-------|-------------|
| Client file | `BepInEx/plugins/YellowFlareCurse/logs/yellowflarecurse-*.log` |
| Client console | BepInEx `LogOutput.log` + Unity player log |
| Server console | SPT server window / `user/logs/spt/` |
| Server file | `user/mods/YellowFlareCurse/logs/yellowflarecurse-server-*.log` |

F12 **Debug** logs every flare success with template id.

## Config (server)

Edit `SPT/user/mods/YellowFlareCurse/config.json`:

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
| AirdropDelaySeconds | 600 | Delay before the airdrop (ignored when map has no points) |
| ShowCountdown | true | Banner + countdown (bottom-right) |
| IncludePlayerGroup | true | Also mark teammates as enemies for cursed bots |
| TeleportBotsNearPlayer | true | Pull scav AI near you on curse start |
| TeleportMinRadius | 100 | Min ring radius (m) |
| TeleportMaxRadius | 150 | Max ring radius (m) |
| AiAlliance | true | Scavs stop fighting each other during curse |
| SpawnTagilla | true | Spawn Tagilla on curse start |
| TagillaType | Factory | `Factory` (`bossTagilla`) or `Labyrinth` (`bossTagillaAgro`) |
| TagillaSpawnMinRadius | 60 | Min Tagilla/cultist placement ring (m) |
| TagillaSpawnMaxRadius | 75 | Max Tagilla/cultist placement ring (m) |
| SpawnCultists | false | Spawn cultist priest + warriors on curse start |
| CultistEscortCount | 4 | Warrior escorts with the priest (1–8) |
| Debug | false | Verbose logging |

## Compatibility

- **Scav Population** — new waves get cursed on the refresh timer
- **SAIN** optional — recommended for aggressive hunting
- **Fika** — host must fire the flare (`IsYourPlayer`); client `InitAirdrop` / boss spawn are host-only
- Indoor / special maps without airdrop points still get the hunt + optional bosses

## Build from source

Requires **.NET 9/10** SDK. Client needs hollowed refs under `Client/References/` (see that folder's README).

```bash
dotnet build YellowFlareCurse.sln -c Release
```

Output is copied to `Build/SPT/` and `Build/BepInEx/` (Forge-ready layout).

## License

MIT — see [LICENSE](LICENSE).
