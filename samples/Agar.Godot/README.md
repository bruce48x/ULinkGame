# Agar.Godot

This is a Godot .NET sample for ULinkGame.

It follows the same top-level shape as `samples/Agar.Unity`: shared contracts and gameplay code live in `Shared`, server projects live in `Server`, and the engine client lives in `Client`.

## Layout

```txt
samples/Agar.Godot/
  Shared/
  Server/
    Gateway/
    State/
    State.Contracts/
  Client/
    project.godot
    Agar.Godot.csproj
    Scenes/
      Main.tscn
    Scripts/
      Networking/
      Rpc/
      Main.cs
```

## Dependencies

- `ULinkGame.Client` NuGet package
- `ULinkRPC.Client`, `ULinkRPC.Serializer.MemoryPack`, `ULinkRPC.Transport.WebSocket`, and `ULinkRPC.Transport.Kcp` NuGet packages
- `src/ULinkGame.Server`
- local `Shared`

## Run

To run the server side, start the Gateway process:

```powershell
dotnet run --project samples/Agar.Godot/Server/Gateway/Gateway.csproj
```

Open `samples/Agar.Godot/Client` in Godot 4 .NET and run the main scene. The client connects to the gateway control WebSocket at `127.0.0.1:20000/ws`, starts guest matchmaking, attaches to the KCP realtime endpoint returned by the server, renders pushed `WorldState` snapshots, and submits WASD input to the server.

The sample keeps its lightweight actor state in-process through `ULinkGame.Server.Actors`; no separate silo or external persistence service is required.
