# Wire protocol v1 (breaks compatibility with the prototype)

TCP port 8209 by default. Every packet is a 4-byte unsigned big-endian byte
length, followed by that many UTF-8 JSON bytes (1–16384 bytes). Reads consume
exactly one frame, regardless of TCP fragmentation/coalescing. Each socket has
one writer and a bounded outgoing queue. A slow receiver is disconnected on
queue overflow or a 5-second write timeout. An unjoined socket has 10 seconds
to send its first complete frame; at most 64 concurrent sockets are accepted.

Client requests use `version: 1`, `command`, and these fields:

| Command | Fields | Allowed |
| --- | --- | --- |
| `join` | `name` | First packet, lobby only |
| `start` | `revision` | Host, full lobby |
| `kill` | `target`, `revision` | Living Mafia, night |
| `vote` | `target`, `revision` | Living player, day, once |
| `restart` | `revision` | Host, finished game |

`target` is a server-assigned numeric player ID. Identity always comes from the
socket, never a request field. Unknown JSON properties, bad JSON/types, null
packets and invalid lengths close the connection. Invalid game actions receive
`error` with a message and leave the game unchanged. A rejected join receives
`error` then closes. Disconnect policy applies to malformed packets from joined
players, too. `revision` changes at every phase transition and restart; stale
commands fail even when a later round has the same phase name.

Server packets have `version: 1` and `type`:

- `joined`: `id` is the connection's player ID.
- `state`: `state` is a full personalized snapshot after each successful action,
  join, or disconnect. It includes phase, revision, host ID, winner, public
  players (id/name/alive/connected), `yourRole`, and `canAct`. `yourRole` is null
  in the lobby. Only the recipient's role is serialized; public players never
  contain roles. Phase and role enums are strings.
- `ok`: the requesting connection's command succeeded, followed by new states.
- `error`: the requesting connection's command failed, with `message`.

Unused response fields may be null. Clients must handle states at any time;
there is no fixed story/prompt packet sequence. No player-to-player chat,
authentication, reconnection tokens, or persistence is provided. Traffic is plain
TCP; use a trusted LAN or an encrypted tunnel for remote games. Private state
routing protects roles from other game clients, not the host or network capture.
