## Shroud

fog-checkbox =
    .label = Nebel des Krieges
    .description = Sicht ist erforderlich, um feindliche Streitkräfte zu sehen

explored-map-checkbox =
    .label = Aufgedeckte Karte
    .description = Die Karte ist initial erkundet


## ServerListLogic

players-online = {$players ->
    *[other] {$players} Spieler Online
}


## ProductionTooltipLogic

requires = Benötigt { $prereqs }


## Generic Names

tank = Panzer
    .gender = masculine

structure = Gebäude
    .gender = other

soldier = Soldat
    .gender = masculine


## Tooltip

enemy-tooltip =
    { $gender ->
        [masculine] Feindlicher { $generic-name }
        [feminine] Feindliche { $generic-name }
        [other] Feindliches { $generic-name }
    }

neutral-tooltip =
    { $gender ->
        [masculine] Neutraler { $generic-name }
        [feminine] Neutrale { $generic-name }
        [other] Neutrales { $generic-name }
    }

allied-tooltip =
    { $gender ->
        [masculine] Allierter { $generic-name }
        [feminine] Allierte { $generic-name }
        [other] Alliertes { $generic-name }
    }


## mainmenu.yaml

singleplayer = Einzelspieler
multiplayer = Mehrspieler
settings = Einstellungen
extras = Extras
manage-content = Inhaltsverwaltung
quit = Beenden
