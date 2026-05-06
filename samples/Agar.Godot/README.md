# Agar.Godot

This is a Godot .NET client sample for ULinkGame.

It is intentionally smaller than `samples/Agar.Unity`: the sample opens a local Godot scene, runs the shared agar simulation offline, and references reusable ULinkGame client helpers from `src/ULinkGame.Client`.

## Layout

```txt
samples/Agar.Godot/
  project.godot
  Agar.Godot.csproj
  Scenes/
    Main.tscn
  Scripts/
    Main.cs
```

## Dependencies

- `src/ULinkGame.Client`
- `samples/Agar.Unity/Shared`

The shared gameplay kernel currently lives in the Unity sample because it is sample-owned business code. This Godot sample references it to prove the gameplay contracts are reusable from another engine while framework code remains under `src`.

## Run

Open `samples/Agar.Godot` in Godot 4 .NET and run the main scene.

The sample is an offline client-side playground. It does not start the Agar Unity sample server or implement RPC transport wiring yet.
