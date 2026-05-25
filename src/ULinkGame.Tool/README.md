# ULinkGame.Tool

`ULinkGame.Tool` 是 ULinkGame 的项目管理工具，而不是宿主运行时库。

它的职责是：

- 初始化项目管理配置
- 统一执行项目级维护命令
- 作为后续 `ULinkGame.Server` 工具链的命令入口

当前已提供：

- `new`

## new

命令参数面向 ULinkGame 项目创建，并会把可兼容的参数转发给 `ulinkrpc-starter`：

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport kcp --network-profile simple --serializer memorypack --persistence none --nugetforunity-source embedded
```

该命令会先调用 `ulinkrpc-starter new --no-next-steps` 生成原始 ULinkRPC 项目骨架，然后在其基础上补充 `ULinkGame.Server` 宿主设施、`ULinkActor` 进程内 actor runtime 根基引用与 `ULinkGame.Client` 客户端包引用，并只输出 ULinkGame 项目的最终 Next steps。默认 `--network-profile simple` 只生成一个 RPC endpoint；需要控制连接和实时连接拆分时，显式传入 `--network-profile realtime`。

- `src/ULinkGame.Server/`
- `src/ULinkGame.Client/`
- `Server/Edge/` in generated sample projects
- 基于 `ULinkGame.Server` 的 edge host 启动代码
- 使用独立的 `ULinkActor` / `ULinkActor.SourceGenerator` 包作为服务端 actor 执行根基
- 客户端项目中的 `ULinkGame.Client` 包引用
- 保留 `ULinkRPC.Analyzers` source generator 路线，不再生成或提交 `Generated/` RPC 源码
- `ulinkgame.tool.json`

默认生成项目不预设 PostgreSQL、MySQL、Redis、SQL Server、Oracle 等任何持久化方案。需要数据库基础设施时，显式传入：

```bash
ulinkgame-tool new --name MyGame --persistence postgres
ulinkgame-tool new --name MyGame --persistence mysql
```

`--persistence postgres` 会为服务端项目加入 `Dapper` / `Npgsql` 包引用；`--persistence mysql` 会加入 `Dapper` / `MySqlConnector` 包引用。连接字符串、业务表、账号系统、背包、排行榜等游戏数据 schema 仍由项目自行定义。

最终会在输出目录下生成：

- `ulinkgame.tool.json`

默认行为：

```bash
ulinkgame-tool new
```

默认输出目录是当前目录。未传 `--output` 时会在当前目录创建项目目录；传入 `--output` 时会在指定目录下创建项目目录。

前提：

- `ulinkrpc-starter` 需要已安装并可被命令行找到

## RPC Source Generation

`ULinkGame.Tool` 不再提供 `codegen` 命令。共享 RPC 契约变更后，通过正常构建触发 `ULinkRPC.Analyzers`：

- 服务端、Godot 客户端：运行对应 `.csproj` 的 `dotnet build` / `dotnet run`。
- Unity / Tuanjie 客户端：打开或重新编译编辑器项目，带有 `[assembly: ULinkRPCGenerateClient("Rpc.Generated")]` 的脚本程序集会接收生成的客户端 API。

## Config Example

```json
{
  "project": {
    "name": "MyGame",
    "clientEngine": "unity",
    "transport": "kcp",
    "networkProfile": "simple",
    "serializer": "memorypack",
    "persistence": "none",
    "nuGetForUnitySource": "embedded"
  }
}
```

## 定位

`ULinkGame.Tool` 不应承载运行时宿主逻辑。

运行时能力属于：

- `ULinkGame.Server`
- `ULinkGame.Client`

项目工具能力属于：

- `ULinkGame.Tool`

## 依赖关系

`ULinkGame.Tool` 对外只依赖：

- `ulinkrpc-starter`

它不会直接调用 `ulinkrpc-codegen`，也不会创建本地 codegen 工具清单。
