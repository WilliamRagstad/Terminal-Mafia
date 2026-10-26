# Mafia rules and hosting controls

Hosts choose a server name, 3–16 players, and counts for Mafia, Police, Doctors,
Detectives and Villagers. Counts must be nonnegative and total exactly the player
count. There must be at least one Mafia and strictly more non-Mafia than Mafia.
Settings are public; assigned roles are private. The default quick game uses one
Mafia and remaining Villagers. The interactive setup defaults to about 1/4 Mafia.

The oldest connected player is host, including if eliminated. The host starts
when the lobby is full. Roles are shuffled anew for each game. Night comes first.

## Night

Every living non-Villager chooses exactly one action. Choices resolve together
when all required players have submitted, independent of submission order:

| Role | Command | Effect |
| --- | --- | --- |
| Mafia | `kill ID` | Vote for one non-Mafia victim; unique plurality wins, ties kill nobody. |
| Police | `block ID` | Block a player's Mafia, Doctor or Detective action for this night. |
| Doctor | `protect ID` | Prevent the Mafia kill on this player; self-protection is allowed. |
| Detective | `investigate ID` | Privately learn whether this player is Mafia. |
| Villager | None | Wait for day. |

Police orders resolve simultaneously and cannot stop other Police orders. They
may target any other living player, including Villagers or Police (no effect on
those roles). Next, unblocked Doctors protect their targets. Unblocked Mafia
votes select at most one victim; protection prevents that kill. Detectives get
their result, or a private "investigation blocked" message. All these effects
resolve even if the actor is killed that night. Blocks and protection expire at
dawn. Only Doctors may target themselves. Repeated targets on later nights are
allowed. No one may act twice in the same phase.

Mafia privately see their teammates' IDs and cannot kill them at night. Detective
results go only to that Detective; the latest 16 are retained until restart.
Other players do not receive targets, results, protection or block details.

## Day and victory

Every living player votes once for another living player with `vote ID`. A unique
plurality is eliminated; a tie eliminates nobody. Then night begins again.
No abstentions, vote changes, timers, or automatic actions. Discuss outside the
client before voting. Connected idle players can hold a phase open.

Villagers (including Police, Doctors and Detectives) win when no Mafia remains.
Mafia win when living Mafia are at least as numerous as all living non-Mafia
combined. Dead players spectate. Roles are never publicly revealed, even at game
end. The server validates all actions, resolves deaths and determines the winner.

## Disconnect and restart

A lobby disconnect frees a seat. During play, disconnect means immediate
elimination, with win detection. Actions/votes by or targeting the disconnected
player are removed, and affected players may act again. Required actions are
re-evaluated, so a disconnected special role cannot hold night open. There is no
reconnect identity or midgame joining. Host ownership transfers to the oldest
connected player.

After victory, the host can `restart` into a lobby; connected players keep their
seats, all roles/actions/deaths/private reports are cleared, and replacements may
join. The server name and configured role counts persist. `start` assigns fresh
roles from those counts. When everyone leaves, the same configured lobby resets
automatically. To change settings, relaunch the server; process restart loses
all state and connections.
