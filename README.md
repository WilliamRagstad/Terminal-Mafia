![](/Assets/shaded_banner.png)

# Terminal Mafia

A playable terminal party game for **3–16 players**, with configurable counts of
Mafia, Police, Doctors, Detectives and Villagers. The server owns roles, phases, votes, elimination and victory.
See [rules and disconnect behavior](docs/RULES.md) and [protocol](docs/PROTOCOL.md).
Hosting supports the original role-count controls; the wire format is version 2.

## Clean install

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
for your OS (Windows, macOS or Linux), plus Git. Then:

```sh
git clone https://github.com/WilliamRagstad/Terminal-Mafia.git
cd Terminal-Mafia
dotnet build "Terminal Mafia.sln" -c Release
```

No third-party NuGet packages or external services are required.

## Host and join

For the original-style hosting setup, launch without arguments:

```sh
dotnet run --project TMServer -c Release --no-build
```

The setup prompts for server name, players, Mafia, Police, Doctors, Detectives,
Villagers, port and bind address. Enter accepts the shown default; invalid input
is retried. Role counts must total the player count, with at least one Mafia and
fewer Mafia than non-Mafia. Use `0.0.0.0` as the bind address for LAN hosting.
Settings are shown to joining players and survive in-game restart.

For scripted hosting, the same controls are available as options:

```sh
dotnet run --project TMServer -c Release --no-build -- --name "Friday Mafia" --players 8 --mafia 2 --police 1 --doctors 1 --detectives 1 --villagers 3 --port 8209 --bind 0.0.0.0
```

Omit `--villagers` to fill the remaining seats automatically. Special roles
otherwise default to zero and Mafia defaults to one. `--help` lists all options.

The earlier positional command still starts a five-player local game with one
Mafia and four Villagers:

```sh
dotnet run --project TMServer -c Release --no-build -- 5 8209 127.0.0.1
```

For friends on your LAN, bind to `0.0.0.0` instead of `127.0.0.1`, allow inbound
TCP port 8209 in your firewall, and share your host computer's LAN IP address.
Use a trusted LAN or encrypted tunnel: this minimal protocol has no encryption
or authentication. No external IP lookup or router configuration is needed for
local play.

Each player opens a separate terminal (including the host if playing):

```sh
dotnet run --project "Terminal Mafia" -c Release --no-build -- 127.0.0.1 Alice 8209
```

Replace `127.0.0.1` with the host's LAN IP when playing across computers. Choose
a unique name of 1–24 ASCII letters or digits. Repeat with different names until
the configured number of players has joined. For a quick local game, host with
`3` and open three client terminals.

The first joined player is host. Enter `start` when full. Each client displays
its private role, public player IDs, current phase, and available action:

- Mafia at night: `kill 2` targets living player #2.
- Police at night: `block 2` prevents that player's non-Police night action.
- Doctors at night: `protect 2` prevents a Mafia kill; self-protection is allowed.
- Detectives at night: `investigate 2` privately checks Mafia affiliation.
- Living players by day: discuss in person or voice chat, then `vote 2`.
- After victory, the host enters `restart`; refill empty seats, then `start`.
- `quit` or closing the client disconnects. During play this eliminates you.

Multiple Mafia vote for one nightly victim (ties kill nobody); all special-role
actions resolve together. See [full role rules](docs/RULES.md). Day votes are final; a unique plurality is eliminated, a tie kills nobody.
The town wins when all Mafia die; Mafia wins at parity with all non-Mafia roles. Dead players spectate.
There are no timers: each required player must act or disconnect. If the host
leaves, the oldest connected player becomes host. Ctrl+C stops the server;
relaunching it starts a fresh lobby and players must reconnect.

## Publish without source

```sh
dotnet publish TMServer -c Release -o artifacts/server
dotnet publish "Terminal Mafia" -c Release -o artifacts/client
```

Copy the whole appropriate output folder to a computer with the .NET 10 runtime:

```sh
dotnet artifacts/server/TMServer.dll 5 8209 0.0.0.0
dotnet "artifacts/client/Terminal Mafia.dll" 127.0.0.1 Alice 8209
```

For computers without .NET, publish separately for each OS/architecture with,
for example, `-r win-x64 --self-contained true` (or `linux-x64`, `osx-arm64`).
Run the resulting executable instead of `dotnet ...dll`.

## Verify

Install Python 3 for the TCP playthrough tests; all tests use standard libraries.
Run from the repository root:

```sh
dotnet build "Terminal Mafia.sln" -c Release
dotnet run --project TMTests -c Release --no-build
python3 tests/playthrough.py
```

On Windows, use `python` instead of `python3` if needed. The playthrough starts
servers on ephemeral loopback ports, plays both wins, checks private snapshots
and rejected actions, restarts on the same sockets, exercises disconnects and
malformed/fragmented frames, and launches the real console client. Override
`DOTNET`, `MAFIA_SERVER` or `MAFIA_CLIENT` to test a specific SDK or published DLLs.

Projects: `TMGame` is the independent rules engine, `TMProtocol` contains framing
and wire DTOs, `TMServer` hosts TCP sessions, `Terminal Mafia` is the client,
and `TMTests` runs rules tests. GitHub Actions builds and runs both test suites.
