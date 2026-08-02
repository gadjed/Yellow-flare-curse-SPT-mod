# Changelog

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
