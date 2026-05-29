# ULinkGame.Server.Hotfix.Generators

Source generators for ULinkGame server hotfix systems.

The first generator slice discovers `[HotfixState]` partial classes and emits generated friend accessors for private fields.

```csharp
[HotfixState]
public partial class PlayerActor
{
    private int exp;
}
```

Generates an editor-hidden accessor similar to:

```csharp
public int __hotfix_exp()
{
    return exp;
}
```

Types marked `[HotfixState]` must be partial. Nested hotfix state also requires partial containing types. Compiler-generated backing fields, static fields, and const fields are ignored.

Full wrapper discovery from hotfix project method declarations is intentionally staged after the first runtime integration. Current samples use explicit stable wrappers while the generator supplies friend accessors.
