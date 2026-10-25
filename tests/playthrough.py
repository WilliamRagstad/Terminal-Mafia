#!/usr/bin/env python3
"""Black-box protocol tests. Python 3 standard library only."""
import json
import os
from pathlib import Path
import signal
import socket
import struct
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
DOTNET = os.environ.get('DOTNET', 'dotnet')
SERVER = Path(os.environ.get('MAFIA_SERVER', ROOT / 'TMServer/bin/Release/net10.0/TMServer.dll'))
CLIENT = Path(os.environ.get('MAFIA_CLIENT', ROOT / 'Terminal Mafia/bin/Release/net10.0/Terminal Mafia.dll'))

class Client:
    def __init__(self, port, name):
        self.sock = socket.create_connection(('127.0.0.1', port), timeout=3)
        self.history = []
        self.send(command='join', name=name)
        joined = self.until(lambda p: p['type'] in ('joined', 'error'))
        assert joined['type'] == 'joined', joined
        self.id = joined['id']
        self.state = self.until(lambda p: p['type'] == 'state')['state']

    def send(self, **packet):
        data = json.dumps(dict(version=1, **packet)).encode()
        self.sock.sendall(struct.pack('!I', len(data)) + data)

    def read(self):
        def exact(n):
            result = b''
            while len(result) < n:
                chunk = self.sock.recv(n - len(result))
                if not chunk:
                    raise EOFError('server closed')
                result += chunk
            return result
        packet = json.loads(exact(struct.unpack('!I', exact(4))[0]))
        self.history.append(packet)
        if packet['type'] == 'state':
            self.state = packet['state']
            # No role field on any public player record, even after death/victory.
            for player in self.state['players']:
                assert set(player) == {'id', 'name', 'alive', 'connected'}, player
        return packet

    def until(self, predicate):
        for _ in range(100):
            packet = self.read()
            if predicate(packet):
                return packet
        raise AssertionError('Expected packet not received')

    def sync(self, revision):
        if self.state['revision'] < revision:
            self.until(lambda p: p['type'] == 'state' and p['state']['revision'] >= revision)
        assert self.state['revision'] == revision

    def act(self, command, target=None, error=False, revision=None):
        self.send(command=command, target=target, revision=self.state['revision'] if revision is None else revision)
        result = self.until(lambda p: p['type'] in ('ok', 'error'))
        assert result['type'] == ('error' if error else 'ok'), result

    def close(self):
        self.sock.close()

class Hosting:
    def __enter__(self):
        # Let the OS pick a port; parse the actual bound endpoint from server stdout.
        self.process = subprocess.Popen([DOTNET, str(SERVER), '5', '0'], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        line = self.process.stdout.readline()
        assert line.startswith('Listening on '), line
        self.port = int(line.split(';')[0].rsplit(':', 1)[1])
        self.clients = []
        return self

    def join(self, name):
        client = Client(self.port, name)
        self.clients.append(client)
        return client

    def __exit__(self, *args):
        for client in self.clients:
            client.close()
        self.process.send_signal(signal.SIGINT)
        try:
            _, errors = self.process.communicate(timeout=8)
        except subprocess.TimeoutExpired:
            self.process.kill()
            raise AssertionError('Server did not shut down')
        assert self.process.returncode == 0, errors


def sync(clients, revision):
    for client in clients:
        client.sync(revision)


def play(clients, town_wins):
    host = clients[0]
    rev = host.state['revision']
    host.act('start')
    rev += 1
    sync(clients, rev)
    mafia, = [c for c in clients if c.state['yourRole'] == 'Mafia']
    town = [c for c in clients if c != mafia]
    assert all(c.state['yourRole'] == 'Villager' for c in town)
    town[0].act('kill', mafia.id, error=True)
    mafia.act('vote', town[0].id, error=True)
    mafia.act('kill', mafia.id, error=True)
    mafia.act('kill', 999, error=True)
    mafia.act('kill', town[0].id, error=True, revision=rev - 1)
    mafia.act('restart', error=True)
    mafia.act('kill', town[0].id)
    rev += 1
    sync(clients, rev)
    town[0].act('vote', mafia.id, error=True)
    living = [c for c in clients if c != town[0]]
    target = mafia if town_wins else town[1]
    for index, client in enumerate(living):
        client.act('vote', target.id if client != target else next(c.id for c in living if c != target))
        if index == 0:
            client.act('vote', target.id, error=True)
    rev += 1
    sync(clients, rev)
    if not town_wins:
        mafia.act('kill', town[2].id)
        rev += 1
        sync(clients, rev)
    for client in clients:
        assert client.state['phase'] == 'Finished'
        assert client.state['winner'] == ('Villagers' if town_wins else 'Mafia')
        assert client.state['yourRole'] == ('Mafia' if client == mafia else 'Villager')
    clients[1].act('restart', error=True)
    host.act('restart')
    sync(clients, rev + 1)
    for client in clients:
        assert client.state['phase'] == 'Lobby' and client.state['yourRole'] is None
        assert all(p['alive'] for p in client.state['players'])


def main():
    with Hosting() as h:
        clients = [h.join('Player' + str(i)) for i in range(5)]
        play(clients, True)
        play(clients, False)
        print('PASS: both victories, secrets, invalid actions, restart on same sockets')

    with Hosting() as h:
        first = h.join('First')
        second = h.join('Second')
        first.close()
        second.until(lambda p: p['type'] == 'state' and p['state']['host'] == second.id)
        clients = [second] + [h.join('New' + str(i)) for i in range(4)]
        second.act('start')
        sync(clients, 1)
        mafia = next(c for c in clients if c.state['yourRole'] == 'Mafia')
        mafia.close()
        remaining = [c for c in clients if c != mafia]
        sync(remaining, 2)
        assert all(c.state['winner'] == 'Villagers' for c in remaining)
        remaining[0].act('restart')
        sync(remaining, 3)
        replacement = h.join('Replacement')
        clients = remaining + [replacement]
        remaining[0].act('start')
        sync(clients, 4)
        for c in clients:
            c.close()
        # Wait for asynchronous EOF handling; failed early joins are closed by Client's caller below.
        deadline = time.monotonic() + 3
        while True:
            probe = socket.create_connection(('127.0.0.1', h.port), timeout=3)
            data = json.dumps({'version': 1, 'command': 'join', 'name': 'Fresh'}).encode()
            probe.sendall(struct.pack('!I', len(data)) + data)
            reader = Client.__new__(Client)
            reader.sock, reader.history = probe, []
            packet = reader.read()
            probe.close()
            if packet['type'] == 'joined':
                break
            assert time.monotonic() < deadline
            time.sleep(.01)
        print('PASS: lobby host transfer, Mafia disconnect, replacement, empty reset')

    with Hosting() as h:
        cli = subprocess.Popen([DOTNET, str(CLIENT), '127.0.0.1', 'ConsolePlayer', str(h.port)],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            assert 'Commands:' in cli.stdout.readline()
            assert 'Joined as #' in cli.stdout.readline()
            output, errors = cli.communicate('quit\n', timeout=5)
            assert cli.returncode == 0, errors
        finally:
            if cli.poll() is None:
                cli.kill()
                cli.wait()
        print('PASS: real terminal client joins and quits')

    with Hosting() as h:
        # Fragmented header/body; coalesced requests; malformed lengths and JSON.
        s = socket.create_connection(('127.0.0.1', h.port), timeout=3)
        data = json.dumps({'version': 1, 'command': 'join', 'name': 'Split'}).encode()
        frame = struct.pack('!I', len(data)) + data
        for byte in frame:
            s.sendall(bytes([byte]))
        c = Client.__new__(Client)
        c.sock, c.history = s, []
        assert c.read()['type'] == 'joined'
        assert c.read()['type'] == 'state'
        data = json.dumps({'version': 1, 'command': 'kill', 'target': 1}).encode()
        frame = struct.pack('!I', len(data)) + data
        s.sendall(frame * 2)
        assert c.read()['type'] == 'error' and c.read()['type'] == 'error'
        s.close()
        for frame in [struct.pack('!I', 20000), struct.pack('!I', 1) + b'{', struct.pack('!I', 4) + b'null']:
            with socket.create_connection(('127.0.0.1', h.port), timeout=3) as bad:
                bad.sendall(frame)
                assert bad.recv(1) == b''
        # Server remains usable after hostile frames.
        h.join('Healthy')
        print('PASS: fragmented/coalesced frames and malformed input isolation')

if __name__ == '__main__':
    main()
