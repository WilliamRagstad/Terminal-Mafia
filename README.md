![](/Assets/shaded_banner.png)

## About

A text game based on the mafia party game.

## How to play

First you need to start the server. Run the `TMServer.exe` file in `TMServer\bin\Debug\netcoreapp3.1` and configure the game settings. The server must be named, pick something that uniquely identifies your game from others. Then specify how many players to allow in the game session, anything less than three is *boring*. Last but not least, you must decide how many players should have each role, these are later assigned at random for each session.

After this is done, all players may join using the ip-address shown in the server window. All games are hosted on port `8209`.

## File structure

This projects contains the two actors needed for a game, the **Server** and **Client**.

### Server

The server is located in the folder `TMServer`.

### Client

The client is located in the folder `Terminal Mafia`.

### Protocol

The protocol is located in the folder `TMProtocol`.