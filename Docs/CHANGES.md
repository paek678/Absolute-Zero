# Change Log

> Record changes on script modification.
> Format: [YYYY-MM-DD] [Author] [Category] - Description

---

[2026-07-13] [Claude] [UI] — Lobby UI integration. AZLobbyUI (runtime-built, no Inspector wiring) with create/join/start/leave flow. TMP Essential Resources imported. NetworkManager UnityTransport wired.

[2026-07-13] [Claude] [Gameplay] — Turn system greybox demo. AbsoluteZeroTurnManager (NetworkBehaviour state machine), AZGameUI (runtime-built UI with temp bars, timer, action buttons), AZPlayerVisual (temp-based capsule coloring). LobbyScene + GameScene created via MCP with all objects wired. Player prefab + NetworkPrefabsList configured.

[2026-07-13] [Claude] [Network] — Network infrastructure migrated from ArenaCombat_server. 7 packages added (NGO 2.11.2, Transport 2.7.2, Relay, Lobby, Auth, MPPM, Multiplayer Tools). 12 C# files created under Assets/Scripts/ with AbsoluteZero namespace: NetworkConstants, PlayerSpawnPoint3D, LobbyServiceHelper, RelayManager, LobbyManager, SessionManager, PlayerSpawnManager, SceneLoadSyncManager, ScrollableLogDisplay, OutgoingDataLog, LobbyPlayerSlotsUI, LobbyTestUI.

[2026-07-13] [Claude] [Infrastructure] — Full harness system setup. Migrated from AbyssNode: hooks (4), settings, statusline, 25 Unity skills, 6 universal safety rules, 4 domain docs, session continuity protocol.
