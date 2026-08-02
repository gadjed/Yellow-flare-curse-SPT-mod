# Changelog

## 1.4.4

- Stronger scav/PMC hunt after teleport: SAIN now gets **seen + dangerous gunshot heard** (not just last-known place), so bots leave slow Search and commit
- Brief settle delay after mass teleport before aggro apply; bots look toward the player

## 1.4.3

- Fixed Tagilla never receiving player aggro: wait for `BotOwner` Active (retry up to 60s) instead of giving up on first non-Active sighting
- Tagilla spawn `PerfectPos` uses player position, not flare sky coords
- Pending Tagilla aggro also retried on the 5s curse refresh

## 1.4.2

- Fixed bot teleport dumping AI on the player: removed NavMesh fallback to player position
- Ring search now retries multiple angles/radii and rejects samples closer than ~85% of `TeleportMinRadius`
- Tagilla placement uses the player position (not flare sky coords) with the same ring validation
- Retargeted to SPT **4.0.13** (net9 / 4.0.11 packages)

## 1.4.1

- Teleport ring defaults increased from 15–40 m to **100–150 m** (F12 range up to 200 m)

## 1.4.0

- Spawn **Tagilla** on curse start (host/authority): `BossLocationSpawn` near the flare, then NavMesh teleport into a ring around the player
- Tagilla is aggroed onto the player/group (curse refresh includes him)
- Maps **without airdrop points** still start the curse (hunt + Tagilla); airdrop is skipped instead of blocking the event
- F12: `SpawnTagilla`, `TagillaType` (Factory / Labyrinth), `TagillaSpawnMinRadius` / `TagillaSpawnMaxRadius` (60–75 m)

## 1.3.0

- On curse start (host/authority): teleport eligible scav/PMC AI into a ring near the player (NavMesh-snapped)
- During curse: AI ally each other (`RemoveEnemy` + `AddAlly`) and `AddEnemy` AI↔AI is blocked — bots hunt players only
- F12 toggles: `TeleportBotsNearPlayer`, `TeleportMinRadius`, `TeleportMaxRadius`, `AiAlliance`

## 1.2.2

- Fixed curse airdrop crate: fully replace loot response with **SUPPLY / «Ящик техобеспечения»** + ForcedLoot
- Previous toiletPaper/mixed mapping always spawned COMMON / «Ящик общей поддержки» (SPT default case)

## 1.2.1

- Fixed SAIN bridge type name (`SAIN.SAINEnableClass`) — curse now marks EnemyKnown so bots hunt under SAIN
- Pause QuestingBots objectives on cursed bots (`StopQuesting`) so PMC/PScavs leave quest paths
- Curse logs now report `sainKnown` / `qbStop` counts

## 1.2.0

- Fixed RSP-30 Yellow trigger: match ammo template `624c09e49b98e019a3315b66`, not the handheld weapon id
- Stronger curse aggro: `ReportAboutEnemy` + `CalcGoalForBot`, optional SAIN known-place bridge, 5s refresh
- Airdrop loot: map curse container to forced Supply-style profile instead of mixed/Common («общей поддержки»)
- Expanded default ForcedLoot (bitcoins, LEDX, GPUs, intel, cases, keycards, top ammo, money)
- Logging to file, BepInEx/Unity console, and SPT server console
- Bottom-right event banner + countdown; overlay hides ~5s after airdrop inbound

## 1.1.1

- Earlier SPT packaging / metadata updates

## 1.1.0 / 1.0.x

- Initial yellow flare curse + delayed airdrop

- Fixed bot teleport dumping AI on the player: removed NavMesh fallback to player position
- Ring search now retries multiple angles/radii and rejects samples closer than ~85% of `TeleportMinRadius`
- Tagilla placement uses the player position (not flare sky coords) with the same ring validation
- Retargeted to SPT **4.0.13** (net9 / 4.0.11 packages)

## 1.4.1

- Teleport ring defaults increased from 15–40 m to **100–150 m** (F12 range up to 200 m)

## 1.4.0

- Spawn **Tagilla** on curse start (host/authority): `BossLocationSpawn` near the flare, then NavMesh teleport into a ring around the player
- Tagilla is aggroed onto the player/group (curse refresh includes him)
- Maps **without airdrop points** still start the curse (hunt + Tagilla); airdrop is skipped instead of blocking the event
- F12: `SpawnTagilla`, `TagillaType` (Factory / Labyrinth), `TagillaSpawnMinRadius` / `TagillaSpawnMaxRadius` (60–75 m)

## 1.3.0

- On curse start (host/authority): teleport eligible scav/PMC AI into a ring near the player (NavMesh-snapped)
- During curse: AI ally each other (`RemoveEnemy` + `AddAlly`) and `AddEnemy` AI↔AI is blocked — bots hunt players only
- F12 toggles: `TeleportBotsNearPlayer`, `TeleportMinRadius`, `TeleportMaxRadius`, `AiAlliance`

## 1.2.2

- Fixed curse airdrop crate: fully replace loot response with **SUPPLY / «Ящик техобеспечения»** + ForcedLoot
- Previous toiletPaper/mixed mapping always spawned COMMON / «Ящик общей поддержки» (SPT default case)

## 1.2.1

- Fixed SAIN bridge type name (`SAIN.SAINEnableClass`) — curse now marks EnemyKnown so bots hunt under SAIN
- Pause QuestingBots objectives on cursed bots (`StopQuesting`) so PMC/PScavs leave quest paths
- Curse logs now report `sainKnown` / `qbStop` counts

## 1.2.0

- Fixed RSP-30 Yellow trigger: match ammo template `624c09e49b98e019a3315b66`, not the handheld weapon id
- Stronger curse aggro: `ReportAboutEnemy` + `CalcGoalForBot`, optional SAIN known-place bridge, 5s refresh
- Airdrop loot: map curse container to forced Supply-style profile instead of mixed/Common («общей поддержки»)
- Expanded default ForcedLoot (bitcoins, LEDX, GPUs, intel, cases, keycards, top ammo, money)
- Logging to file, BepInEx/Unity console, and SPT server console
- Bottom-right event banner + countdown; overlay hides ~5s after airdrop inbound

## 1.1.1

- Earlier SPT packaging / metadata updates

## 1.1.0 / 1.0.x

- Initial yellow flare curse + delayed airdrop
