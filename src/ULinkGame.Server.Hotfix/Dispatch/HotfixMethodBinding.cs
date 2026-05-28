using System.Reflection;
using ULinkGame.Server.Hotfix.Abstractions;

namespace ULinkGame.Server.Hotfix.Dispatch;

public sealed record HotfixMethodBinding(
    HotfixMethodKey Key,
    MethodInfo Method,
    Type StateType,
    Type ReturnType,
    IReadOnlyList<Type> ParameterTypes);
