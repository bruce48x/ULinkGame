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

- `src/ULinkGame.Client`
- `src/ULinkGame.Server`
- local `Shared`

## Run

Open `samples/Agar.Godot/Client` in Godot 4 .NET and run the main scene.

The sample is an offline client-side playground. It does not start the Agar Unity sample server or implement RPC transport wiring yet.
