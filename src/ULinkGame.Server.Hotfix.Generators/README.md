# ULinkGame.Server.Hotfix.Generators

Source generators for ULinkGame server hotfix systems.

The first generator slice discovers `[HotfixState]` partial classes and emits generated friend accessors for private fields. Hotfix system method wrapper dispatch is intentionally left for a later generator slice.
