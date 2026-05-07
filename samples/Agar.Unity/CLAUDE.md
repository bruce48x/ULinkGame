# Agar Unity Sample Notes

## Entrypoints

- Shared RPC and gameplay contracts: `Shared/Interfaces/IPlayerService.cs`.
- Shared arena simulation: `Shared/Gameplay/ArenaSimulation.cs`.
- Unity gameplay controller: `Client/Assets/Scripts/Gameplay/DotArenaGame.cs` and its partial files.
- Unity network wrapper: `Client/Assets/Scripts/Gameplay/DotArenaNetworkSession.cs`.
- Control gateway service: `Server/Server/Services/PlayerService.cs`.
- Realtime room runtime: `Server/Server/Realtime/RoomRuntime.cs`.
- Orleans silo entrypoint: `Server/Silo/Program.cs`.
- User persistence grain: `Server/Silo/Users/UserGrain.cs`.
- Leaderboard grain: `Server/Silo/Leaderboard/LeaderboardGrain.cs`.

## Local Workflow

1. Update `docs/GAMEPLAY_DESIGN.md` and any relevant file under `docs/features/` when behavior or architecture changes.
2. Update `docs/DEVELOPMENT_PLAN.md` before implementation when the plan changes.
3. Edit shared contracts first, then regenerate RPC code:
   - From `samples/Agar.Unity`: `dotnet tool run ulinkrpc-codegen -- --mode unity --contracts Shared --output Client/Assets/Scripts/Rpc/Generated --namespace Rpc`
   - From `samples/Agar.Unity`: `dotnet tool run ulinkrpc-codegen -- --mode server --contracts Shared --server-output Server/Server/Generated --server-namespace Server.Generated`
4. Build affected .NET projects:
   - `dotnet build Shared/Shared.csproj -f net10.0`
   - `dotnet build Server/Silo/Silo.csproj`
   - `dotnet build Server/Server/Server.csproj`
5. Run `dotnet test tests/BusinessLogic.Tests/BusinessLogic.Tests.csproj`.

## Gameplay Baseline

- The active rules are Agar-style growth and consumption: collect mass food, grow radius/mass, slow down as mass grows, consume smaller players, respawn, then rank by score/mass.
- Input contains only movement and tick data. There is no dash, stun, shield, knockback, or speed boost gameplay in the active protocol.
- Food pickup type is `ScorePoint`; all food grants score/mass through the shared simulation.
- Multiplayer match settlement awards weekly victory points by rank: 1st 10, 2nd 7, 3rd 5, 4th 3, 5th 1. AI players with the `AI` prefix do not receive victory points.
- The leaderboard is queried through `IPlayerService.GetLeaderboardAsync` and served by singleton `ILeaderboardGrain` key `0`.
- `LeaderboardGrain` maintains a current-period index written during match settlement. It does not scan all user grains because the sample does not yet have a full user directory.

## Runtime Notes

- Control-plane RPC uses WebSocket. Realtime RPC uses KCP when available.
- `AttachRealtimeAsync` binds the realtime connection to the room runtime owner gateway.
- PostgreSQL-backed Orleans storage is used for users, sessions, matchmaking, rooms, and leaderboards.
- `DisconnectedSessionCleanupHostedService` owns periodic cleanup of stale local session registrations.
- Current verification baseline: `dotnet build Shared/Shared.csproj`, `dotnet build Server/Silo/Silo.csproj`, `dotnet build Server/Server/Server.csproj`, `dotnet test tests/BusinessLogic.Tests/BusinessLogic.Tests.csproj`, and Unity script refresh with no console errors.
