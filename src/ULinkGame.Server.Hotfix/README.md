# ULinkGame.Server.Hotfix

Runtime loader and dispatch infrastructure for server-side ULinkGame hotfix assemblies.

This package keeps reload mechanics separate from actor runtime, sessions, transports, and gameplay code.

## Server hotfix flow

Stable code owns state:

```csharp
[HotfixState]
public partial class PlayerActor : Actor
{
    private int level;
    private int exp;
}
```

Hotfix code owns behavior:

```csharp
[FriendOf(typeof(PlayerActor))]
[HotfixSystemOf(typeof(PlayerActor))]
public static class PlayerActorSystem
{
    public static void AddExp(this PlayerActor self, int amount)
    {
        var exp = self.__hotfix_exp();
    }
}
```

Reload with `IHotfixManager.ReloadAsync()`. Reload failure keeps the previous dispatch table active.

Use `AddULinkGameHotfix(...)` to register a source such as `CurrentDirectoryHotfixAssemblySource`, and pass stable assembly names as shared assemblies so hotfix systems operate on the same state types as the running server. `AddULinkGameHotfixFileWatcher(...)` can be added when a host should reload after hotfix DLL changes.
