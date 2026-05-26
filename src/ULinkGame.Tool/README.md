# ULinkGame.Tool

`ULinkGame.Tool` 用来创建 ULinkGame 项目。

## Install

先确保 `ulinkrpc-starter` 已安装并在 `PATH` 中，然后安装本工具：

```bash
dotnet tool install --global ULinkRPC.Starter
dotnet tool install --global ULinkGame.Tool
```

## Create A Project

推荐先用最少参数生成项目：

```bash
ulinkgame-tool new --name MyGame
```

生成后按命令行输出的 Next steps 启动服务端并打开客户端项目。

常用选项：

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport websocket --serializer json
```

可选值：

- `--client-engine`: `unity`, `unity-cn`, `tuanjie`, `godot`
- `--transport`: `websocket`, `tcp`, `kcp`
- `--serializer`: `json`, `memorypack`
- `--persistence`: `none`, `postgres`, `mysql`
- `--nugetforunity-source`: `embedded`, `openupm`

## Defaults

默认会生成：

- 服务端项目
- Unity/Tuanjie/Godot 客户端项目
- Shared 合约项目
- ULinkGame 服务端和客户端依赖
- cluster-ready 配置骨架
- `ulinkgame.tool.json`

如果需要本地 Docker Compose 演练：

```bash
ulinkgame-tool new --name MyGame --deploy-profile compose
```

如果需要数据库依赖：

```bash
ulinkgame-tool new --name MyGame --persistence postgres
ulinkgame-tool new --name MyGame --persistence mysql
```
