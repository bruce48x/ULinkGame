# ULinkGame.Tool

`ULinkGame.Tool` creates ULinkGame projects.

## Install

Install the tool:

```bash
dotnet tool install --global ULinkGame.Tool
```

The first time you run `ulinkgame-tool new`, the tool automatically installs the matching `ULinkRPC.Starter` version if `ulinkrpc-starter` is not already available, then continues project generation.

## Create A Project

Start with the minimal command:

```bash
ulinkgame-tool new --name MyGame
```

After generation, follow the printed next steps to start the server and open the client project.

Common options:

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport websocket --serializer json
```

Supported values:

- `--client-engine`: `unity`, `unity-cn`, `tuanjie`, `godot`
- `--transport`: `websocket`, `tcp`, `kcp`
- `--serializer`: `json`, `memorypack`
- `--persistence`: `none`, `postgres`, `mysql`
- `--nugetforunity-source`: `embedded`, `openupm`

## Defaults

By default, the generated project includes:

- a server project
- a Unity, Tuanjie, or Godot client project
- a shared contract project
- ULinkGame server and client dependencies
- cluster-ready configuration scaffolding
- `ulinkgame.tool.json`

For a local Docker Compose rehearsal:

```bash
ulinkgame-tool new --name MyGame --deploy-profile compose
```

To include database dependencies:

```bash
ulinkgame-tool new --name MyGame --persistence postgres
ulinkgame-tool new --name MyGame --persistence mysql
```
