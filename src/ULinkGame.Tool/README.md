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

After generation, run the printed check command before starting the server:

```bash
cd MyGame
dotnet run --project "Server/Server/Server.csproj" -- --ulinkgame-check
```

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
- Cluster infrastructure
- Hotfix infrastructure
- Reliable Push infrastructure
- `ulinkgame.tool.json`

For Unity and Tuanjie clients, the tool pins `ULinkGame.Client` and `ULinkGame.Abstractions` in `Assets/packages.config` and generates an editor import guard that prevents NuGet analyzer DLLs from being loaded as Unity runtime plugins.

The generated `appsettings.json` intentionally stays small. It contains only the local node identity and client endpoint binding under `ULinkGame`; cluster services, hotfix defaults, reliable push defaults, and RPC check output are derived by generated server helper code.

For a local Docker Compose rehearsal:

```bash
ulinkgame-tool new --name MyGame --deploy-profile compose
```

To include database dependencies:

```bash
ulinkgame-tool new --name MyGame --persistence postgres
ulinkgame-tool new --name MyGame --persistence mysql
```

## Generated Configuration

The default development appsettings file has this shape:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "dev-1"
    },
    "Endpoint": {
      "Transport": "kcp",
      "Host": "127.0.0.1",
      "Port": 20000
    }
  }
}
```

For WebSocket projects, the endpoint also includes `"Path": "/ws"`.

Validate the derived project state with:

```bash
dotnet run --project "Server/Server/Server.csproj" -- --ulinkgame-check
```

The check prints the generated Cluster, Hotfix, Reliable Push, and RPC state so the default `appsettings.json` does not need to expose every derived setting.

Use JSON output when CI or deployment scripts need machine-readable validation results:

```bash
dotnet run --project "Server/Server/Server.csproj" -- --ulinkgame-check --json
```

## Cluster Configuration

The generated server derives a node-local service model. A node is one .NET server process; generated defaults include gateway, node-directory, and route-directory services inside that node.

The generated development profile derives an all-in-one node equivalent to:

```json
{
  "Cluster": {
    "NodeId": "dev-1",
    "AdvertisedEndpoints": {
      "cluster": "tcp://127.0.0.1:21000",
      "client": "kcp://127.0.0.1:20000"
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
      { "Kind": "gateway", "Name": "gateway" }
    ]
  }
}
```

Production-oriented profiles can keep the same service model, split services across nodes, add project services such as lobby, match, room, or chat as needed, and use persistent node-directory storage:

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
