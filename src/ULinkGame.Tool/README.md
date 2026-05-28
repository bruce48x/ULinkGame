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

## Cluster Configuration

The cluster scaffold uses a node-local service configuration model. A node is one .NET server process; services such as gateway, lobby, match, room, chat, node-directory, and route-directory are configured inside that node.

The generated development profile uses an all-in-one node:

```json
{
  "Cluster": {
    "NodeId": "dev-1",
    "AdvertisedEndpoints": {
      "cluster": "tcp://127.0.0.1:21000",
      "client": "ws://127.0.0.1:20000/ws"
    },
    "Bootstrap": {
      "NodeDirectoryEndpoints": [
        "tcp://127.0.0.1:21000"
      ]
    },
    "NodeDirectory": {
      "Enabled": true,
      "Storage": {
        "Mode": "InMemory"
      }
    },
    "Services": [
      { "Kind": "node-directory", "Name": "node-directory" },
      { "Kind": "route-directory", "Name": "route-directory" },
      { "Kind": "gateway", "Name": "gateway" },
      { "Kind": "lobby", "Name": "lobby" },
      { "Kind": "match", "Name": "match" },
      { "Kind": "room", "Name": "room" },
      { "Kind": "chat", "Name": "chat" }
    ]
  }
}
```

Production-oriented profiles should keep the same shape, split services across nodes as needed, and use persistent node-directory storage:

```json
{
  "Cluster": {
    "NodeId": "control-1",
    "AdvertisedEndpoints": {
      "cluster": "tcp://10.0.0.10:21000"
    },
    "NodeDirectory": {
      "Enabled": true,
      "Storage": {
        "Mode": "Persistent",
        "Provider": "postgres",
        "ConnectionStringName": "ClusterDirectory"
      }
    },
    "Services": [
      { "Kind": "node-directory", "Name": "node-directory" },
      { "Kind": "route-directory", "Name": "route-directory" }
    ]
  }
}
```

Provider names and connection-string keys are configuration guidance; concrete provider references and secret loading remain project-owned deployment choices.
