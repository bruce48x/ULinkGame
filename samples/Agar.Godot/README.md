# Agar.Godot

This is a Godot .NET sample for ULinkGame.

It follows the same top-level shape as `samples/Agar.Unity`: shared contracts and gameplay code live in `Shared`, server projects live in `Server`, and the engine client lives in `Client`.

## Layout

```txt
samples/Agar.Godot/
  Shared/
  Server/
    Edge/
    Silo/
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

To run the server side, start the Orleans silo first, then start the Edge gateway:

```powershell
dotnet run --project samples/Agar.Godot/Server/Silo/Silo.csproj
dotnet run --project samples/Agar.Godot/Server/Edge/Edge.csproj
```

Open `samples/Agar.Godot/Client` in Godot 4 .NET and run the main scene. The client connects to the gateway control WebSocket at `127.0.0.1:20000/ws`, starts guest matchmaking, attaches to the KCP realtime endpoint returned by the server, renders pushed `WorldState` snapshots, and submits WASD input to the server.

Both Silo and Edge projects read their Orleans connection string from `appsettings.json`.
