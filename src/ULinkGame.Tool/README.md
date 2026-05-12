# ULinkGame.Tool

`ULinkGame.Tool` 是 ULinkGame 的项目管理工具，而不是宿主运行时库。

它的职责是：

- 初始化项目管理配置
- 统一执行项目级维护命令
- 作为后续 `ULinkGame.Server` 工具链的命令入口

当前已提供：

- `new`
- `codegen`

## new

命令参数面向 ULinkGame 项目创建，并会把可兼容的参数转发给 `ulinkrpc-starter`：

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport kcp --network-profile simple --serializer memorypack --nugetforunity-source embedded
```

该命令会先调用 `ulinkrpc-starter --no-next-steps` 生成原始 ULinkRPC 项目骨架，然后在其基础上补充 Microsoft Orleans 与 ULinkGame 宿主设施，并只输出 ULinkGame 项目的最终 Next steps。默认 `--network-profile simple` 只生成一个 RPC endpoint；需要控制连接和实时连接拆分时，显式传入 `--network-profile realtime`。

- `src/ULinkGame.Server/`
- `Server/Silo/` in generated sample projects
- `Server/Edge/` in generated sample projects
- 基于 `ULinkGame.Server` 的 edge 启动代码
- Microsoft Orleans 本地开发配置
- `ulinkgame.tool.json`

默认生成项目使用 Orleans localhost clustering 和 memory grain storage，不预设 PostgreSQL、MySQL、Redis、SQL Server、Oracle 等任何持久化方案。生产环境应按项目实际基础设施显式接入 Orleans clustering / storage provider。

最终会在输出目录下生成：

- `ulinkgame.tool.json`

默认行为：

```bash
ulinkgame-tool new
```

默认输出目录是当前目录。未传 `--output` 时会在当前目录创建项目目录；传入 `--output` 时会在指定目录下创建项目目录。

前提：

- `ulinkrpc-starter` 需要已安装并可被命令行找到

## codegen

根据 `ulinkgame.tool.json` 所在项目根目录，恢复本地 .NET 工具并调用 `ulinkrpc-codegen` 重新生成 RPC 代码：

```bash
ulinkgame-tool codegen
```

可选参数：

```bash
ulinkgame-tool codegen --config path/to/ulinkgame.tool.json
ulinkgame-tool codegen --no-restore
```

## Config Example

```json
{
  "project": {
    "name": "MyGame",
    "clientEngine": "unity",
    "transport": "kcp",
    "networkProfile": "simple",
    "serializer": "memorypack",
    "nuGetForUnitySource": "embedded"
  },
  "codegen": {
    "contractsPath": "Shared",
    "server": {
      "projectPath": "Server/Edge",
      "outputPath": "Generated",
      "namespace": "Edge.Generated"
    },
    "unityClient": {
      "projectPath": "Client",
      "outputPath": "Assets/Scripts/Rpc/Generated",
      "namespace": "Rpc.Generated"
    }
  }
}
```

## 定位

`ULinkGame.Tool` 不应承载运行时宿主逻辑。

运行时能力属于：

- `ULinkGame.Server`

项目工具能力属于：

- `ULinkGame.Tool`

## 依赖关系

`ULinkGame.Tool` 对外只依赖：

- `ulinkrpc-starter`

它不会直接调用 `ulinkrpc-codegen`。
