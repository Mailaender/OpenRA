fog-checkbox =
    .label = Fog of War
    .description = Line of sight is required to view enemy forces

explored-map-checkbox =
    .label = Explored Map
    .description = Initial map shroud is revealed


## ServerListLogic

players-online = {$players ->
    [one] {$players} Player Online
   *[other] {$players} Players Online
}


## Generic Names

tank = Tank

structure = Structure

soldier = Soldier


## Tooltip

enemy-tooltip =
    { $gender ->
        *[other] Enemy { $generic-name }
    }

## ProductionTooltipLogic

requires = Requires { $prereqs }


## mainmenu.yaml

singleplayer = Singleplayer
multiplayer = Multiplayer
settings = Settings
extras = Extras
manage-content = Manage Content
quit = Quit
