# Minimal Mafia rules

3–16 players, exactly one randomly assigned Mafia, everyone else a Villager.
Special roles from the prototype are not supported in this version.
The oldest connected player is host, including if eliminated. The host starts
when the lobby is full. The first phase is night.

At night the living Mafia chooses another living player to kill. By day every
living player votes once for another living player. A unique plurality is
eliminated; a tie eliminates nobody. Then night begins again. No abstentions,
self-targets, vote changes, timers, or automatic actions. Discuss outside the
client before voting. Connected idle players can hold a phase open.

Villagers win immediately when no Mafia remains. Mafia wins at parity (one
Villager or fewer). Eliminated players spectate and retain their own role only.
Roles are never publicly revealed, including at game end. The server chooses
roles, validates all actions, resolves deaths, and determines the winner.

A lobby disconnect frees a seat. During play, disconnect means immediate
elimination, with win detection. Votes by or targeting a disconnected player are
removed, and affected voters may vote again. There is no reconnect identity or
midgame joining. Host ownership transfers to the oldest connected player.
After victory, the host can restart into a lobby; connected players keep their
seats, all roles/votes/deaths are cleared, and replacements may join. Start
assigns fresh random roles. When everyone leaves, the lobby resets automatically.
A server process restart loses all state and connections.
