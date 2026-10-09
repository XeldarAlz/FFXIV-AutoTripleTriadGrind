# Custom Cross-Territory Routes

Maps without direct teleport access use the shared route executor. Routes are registered by destination territory ID. After entering the destination, normal navigation takes the player to the NPC. All NPCs in the same map share its entry route.

`Core/Travel/CustomTravelRoutes.cs` defines the built-in routes for Fortemps Manor (433) and Lower Jeuno (1265). Add or override routes through `CustomRoutes` in the plugin configuration JSON. Unload the plugin before editing the configuration file, then reload it after saving. There is currently no route editor UI.

The built-in Lower Jeuno route travels to Yak T'el (1189), navigates to entrance object `2014450` at world coordinates `-528.37415, -152.20874, 671.13745`, then interacts and confirms entry into Lower Jeuno (1265). It goes directly to the entrance and does not require unlocking the additional transport service near Mamook. Access to Lower Jeuno must already be unlocked. The route enters the public map rather than the quest instance (1264), and serves all Triple Triad NPCs in the map.

The following configuration enters Fortemps Manor through the House Fortemps guard in The Pillars (419). Add this property to the existing plugin configuration object:

```json
"CustomRoutes": {
  "433": [
    {
      "SourceTerritoryId": 419,
      "TargetTerritoryId": 433,
      "Position": { "X": 17.990356, "Y": 16.009666, "Z": -9.567444 },
      "EntranceBaseId": 1011217,
      "ApproachMeters": 3,
      "TimeoutMs": 30000,
      "MenuEntryIndex": 0,
      "ConfirmEntry": true
    }
  ]
}
```

- `Position` uses world coordinates, not the coordinates displayed on the map.
- `EntranceBaseId` is the data ID of the entrance NPC or event object. Set it to `0` to walk across a zone boundary. In that case, `Position` must reach the transition trigger, and `TimeoutMs` must allow enough time for the walk.
- `MenuEntryText` selects a menu entry by its full text in the current game language. It takes priority over the zero-based `MenuEntryIndex`. If neither is configured, menu entries are not selected automatically.
- `ConfirmEntry` controls whether entrance confirmation prompts are accepted. The executor advances dialogue only after interacting with the entrance.
- List steps in execution order. Each step's target territory must match the next step's source territory, and the final target must match the configuration key. Each step waits for its map transition to complete.
- Source maps can themselves be reached through configured routes. Circular route dependencies are rejected. Invalid configurations, unavailable entrances, or transition timeouts stop travel and cause the current NPC to be treated as unreachable.

The manor entrance data comes from [Questionable's The Paths We Walk route](https://github.com/afan0431/Questionable/blob/cn/QuestPaths/3.x%20-%20Heavensward/Side%20Stories/Tales%20of%20the%20Dragonsong%20War/1475_The%20Paths%20We%20Walk.json).
