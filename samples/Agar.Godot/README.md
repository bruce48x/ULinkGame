# Agar.Godot

This is a Godot .NET sample for ULinkGame.

It follows the same top-level shape as `samples/Agar.Unity`: shared contracts and gameplay code live in `Shared`, server projects live in `Server`, and the engine client lives in `Client`.

## Layout

```txt
samples/Agar.Godot/
  Shared/
  Server/
  Client/
    project.godot
    Agar.Godot.csproj
    Scenes/
      Main.tscn
    Scripts/
      Main.cs
```

## Dependencies

- `ULinkGame.Client` NuGet package
- `src/ULinkGame.Server`
- local `Shared`

## Run

Open `samples/Agar.Godot/Client` in Godot 4 .NET and run the main scene.

To run the server side, start the Orleans silo first, then start the gateway server:

```powershell
dotnet run --project samples/Agar.Godot/Server/Silo/Silo.csproj
dotnet run --project samples/Agar.Godot/Server/Server/Server.csproj
```

Both server projects read their Orleans connection string from `appsettings.json`.
