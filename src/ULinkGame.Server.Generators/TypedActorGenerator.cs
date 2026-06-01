using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ULinkGame.Server.Generators
{
    [Generator]
    public sealed class TypedActorGenerator : IIncrementalGenerator
    {
        private const string ActorIgnoreAttributeName = "ULinkGame.Server.Actors.ActorIgnoreAttribute";
        private const string ActorLocalOnlyAttributeName = "ULinkGame.Server.Actors.ActorLocalOnlyAttribute";
        private const string ActorMethodAttributeName = "ULinkGame.Server.Actors.ActorMethodAttribute";
        private const string ActorNameAttributeName = "ULinkGame.Server.Actors.ActorNameAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var actors = context.SyntaxProvider
                .CreateSyntaxProvider(
                    IsActorCandidate,
                    GetActor)
                .Where(IsNotNull);

            context.RegisterSourceOutput(actors, GenerateActor);
        }

        private static bool IsActorCandidate(SyntaxNode node, CancellationToken cancellationToken)
        {
            return node is ClassDeclarationSyntax;
        }

        private static ActorInfo? GetActor(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            var declaration = (ClassDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol)
            {
                return null;
            }

            var keyType = GetActorKeyType(symbol);
            if (keyType == null)
            {
                return null;
            }

            var candidateMethods = symbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(IsPublicInstanceOrdinaryMethod)
                .Where(static method => !HasAttribute(method, ActorIgnoreAttributeName))
                .ToArray();
            var methods = candidateMethods
                .Where(IsEligibleMethod)
                .Select(method => MethodInfo.Create(method))
                .ToArray();
            var unsupportedMethods = candidateMethods
                .Where(static method => !IsEligibleMethod(method))
                .Select(static method => new UnsupportedMethodInfo(
                    method.Name,
                    method.Locations.Length == 0 ? Location.None : method.Locations[0]))
                .ToArray();
            var actorName = GetAttributeString(symbol, ActorNameAttributeName) ?? LowerFirst(GetActorPrefix(symbol.Name));
            var isLocalOnly = HasAttribute(symbol, ActorLocalOnlyAttributeName);

            return new ActorInfo(symbol, keyType, actorName, isLocalOnly, methods, unsupportedMethods);
        }

        private static bool IsNotNull(ActorInfo? actor)
        {
            return actor != null;
        }

        private static ITypeSymbol? GetActorKeyType(INamedTypeSymbol symbol)
        {
            for (var current = symbol.BaseType; current != null; current = current.BaseType)
            {
                if (current.Arity == 1 &&
                    current.Name == "Actor" &&
                    current.ContainingNamespace.ToDisplayString() == "ULinkGame.Server.Actors")
                {
                    return current.TypeArguments[0];
                }
            }

            return null;
        }

        private static bool IsEligibleMethod(IMethodSymbol method)
        {
            if (!IsValueTask(method.ReturnType, out _))
            {
                return false;
            }

            if (method.Parameters.Length == 1)
            {
                return true;
            }

            return method.Parameters.Length == 2 &&
                IsCancellationToken(method.Parameters[1].Type);
        }

        private static bool IsPublicInstanceOrdinaryMethod(IMethodSymbol method)
        {
            return method.DeclaredAccessibility == Accessibility.Public &&
                !method.IsStatic &&
                method.MethodKind == MethodKind.Ordinary;
        }

        private static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            return symbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass != null &&
                attribute.AttributeClass.ToDisplayString() == attributeName);
        }

        private static string? GetAttributeString(ISymbol symbol, string attributeName)
        {
            var attribute = symbol.GetAttributes().FirstOrDefault(candidate =>
                candidate.AttributeClass != null &&
                candidate.AttributeClass.ToDisplayString() == attributeName);
            return attribute?.ConstructorArguments.Length == 1
                ? attribute.ConstructorArguments[0].Value as string
                : null;
        }

        private static bool IsValueTask(ITypeSymbol type, out ITypeSymbol? resultType)
        {
            resultType = null;

            if (type is INamedTypeSymbol namedType &&
                namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
                namedType.Name == "ValueTask")
            {
                if (namedType.Arity == 0)
                {
                    return true;
                }

                if (namedType.Arity == 1)
                {
                    resultType = namedType.TypeArguments[0];
                    return true;
                }
            }

            return false;
        }

        private static bool IsCancellationToken(ITypeSymbol type)
        {
            return type.Name == "CancellationToken" &&
                type.ContainingNamespace.ToDisplayString() == "System.Threading";
        }

        private static void GenerateActor(SourceProductionContext context, ActorInfo? actor)
        {
            if (actor == null)
            {
                return;
            }

            foreach (var unsupportedMethod in actor.UnsupportedMethods)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TypedActorGeneratorDiagnostics.UnsupportedMethodSignature,
                    unsupportedMethod.Location,
                    unsupportedMethod.Name));
            }

            var hintName = CreateHintName(actor.Symbol);
            context.AddSource(hintName, SourceText.From(GenerateActorSource(actor), Encoding.UTF8));
        }

        private static string GenerateActorSource(ActorInfo actor)
        {
            var namespaceName = actor.Symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : actor.Symbol.ContainingNamespace.ToDisplayString();
            var prefix = GetActorPrefix(actor.Symbol.Name);
            var keyType = DisplayType(actor.KeyType, actor.Symbol.ContainingNamespace);
            var actorsType = prefix + "Actors";
            var localRefType = prefix + "LocalRef";
            var remoteRefType = prefix + "RemoteRef";

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine();

            if (namespaceName != null)
            {
                builder.Append("namespace ").Append(namespaceName).AppendLine();
                builder.AppendLine("{");
            }

            var indentLevel = namespaceName != null ? 1 : 0;
            AppendActorsClass(builder, actor, actorsType, localRefType, remoteRefType, keyType, indentLevel);
            builder.AppendLine();
            AppendLocalRef(builder, actor, localRefType, keyType, actor.ActorName, indentLevel);
            if (!actor.IsLocalOnly)
            {
                builder.AppendLine();
                AppendRemoteRef(builder, actor, remoteRefType, keyType, actor.ActorName, indentLevel);
                builder.AppendLine();
                AppendClusterHandler(builder, actor, indentLevel);
            }

            if (namespaceName != null)
            {
                builder.AppendLine("}");
            }

            return builder.ToString();
        }

        private static void AppendActorsClass(
            StringBuilder builder,
            ActorInfo actor,
            string actorsType,
            string localRefType,
            string remoteRefType,
            string keyType,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            builder.Append(indent).Append("public sealed class ").Append(actorsType).AppendLine();
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;");
            if (!actor.IsLocalOnly)
            {
                builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IRemoteActorInvoker _remote;");
                builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IRemoteActorSerializer _serializer;");
                builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.RemoteActorOptions _options;");
            }

            builder.AppendLine();
            builder.Append(indent).Append("    public ").Append(actorsType).AppendLine("(");
            builder.Append(indent).Append("        global::ULinkGame.Server.Actors.IActorRuntime runtime");
            if (actor.IsLocalOnly)
            {
                builder.AppendLine(")");
            }
            else
            {
                builder.AppendLine(",");
                builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.IRemoteActorInvoker remote,");
                builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.IRemoteActorSerializer serializer,");
                builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.RemoteActorOptions options)");
            }

            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        _runtime = runtime;");
            if (!actor.IsLocalOnly)
            {
                builder.Append(indent).AppendLine("        _remote = remote;");
                builder.Append(indent).AppendLine("        _serializer = serializer;");
                builder.Append(indent).AppendLine("        _options = options;");
            }

            builder.Append(indent).AppendLine("    }");
            builder.AppendLine();
            builder.Append(indent).Append("    public ").Append(localRefType).Append(" Local(").Append(keyType).AppendLine(" id)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).Append("        return new ").Append(localRefType).AppendLine("(_runtime, id);");
            builder.Append(indent).AppendLine("    }");
            if (!actor.IsLocalOnly)
            {
                builder.AppendLine();
                builder.Append(indent).Append("    public ").Append(remoteRefType).Append(" Remote(global::ULinkGame.Cluster.NodeId nodeId, ").Append(keyType).AppendLine(" id)");
                builder.Append(indent).AppendLine("    {");
                builder.Append(indent).Append("        return new ").Append(remoteRefType).AppendLine("(_remote, _serializer, _options, nodeId, id);");
                builder.Append(indent).AppendLine("    }");
            }

            builder.Append(indent).AppendLine("}");
        }

        private static void AppendLocalRef(
            StringBuilder builder,
            ActorInfo actor,
            string localRefType,
            string keyType,
            string routePrefix,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            builder.Append(indent).Append("public readonly struct ").Append(localRefType).AppendLine();
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;");
            builder.Append(indent).Append("    private readonly ").Append(keyType).AppendLine(" _id;");
            builder.AppendLine();
            builder.Append(indent).Append("    public ").Append(localRefType).Append("(global::ULinkGame.Server.Actors.IActorRuntime runtime, ").Append(keyType).AppendLine(" id)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        _runtime = runtime;");
            builder.Append(indent).AppendLine("        _id = id;");
            builder.Append(indent).AppendLine("    }");

            foreach (var method in actor.Methods)
            {
                builder.AppendLine();
                AppendLocalMethod(builder, actor, method, routePrefix, indentLevel + 1);
            }

            builder.Append(indent).AppendLine("}");
        }

        private static void AppendLocalMethod(
            StringBuilder builder,
            ActorInfo actor,
            MethodInfo method,
            string routePrefix,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            var returnType = DisplayReturnType(actor, method);
            var requestType = DisplayType(method.RequestType, actor.Symbol.ContainingNamespace);
            var actorType = actor.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            builder.Append(indent)
                .Append("public ")
                .Append(returnType)
                .Append(' ')
                .Append(method.Name)
                .Append('(')
                .Append(requestType)
                .Append(" request, global::System.Threading.CancellationToken cancellationToken = default)")
                .AppendLine();
            builder.Append(indent).AppendLine("{");
            builder.Append(indent)
                .Append("    var actorId = global::ULinkGame.Server.Actors.ActorId.From(\"")
                .Append(routePrefix)
                .Append("/\" + ")
                .Append(CreateKeyValueExpression(actor.KeyType))
                .Append(");")
                .AppendLine();

            if (method.ResultType == null)
            {
                builder.Append(indent)
                    .Append("    return _runtime.TellAsync<")
                    .Append(actorType)
                    .Append(">(actorId, (actor, ct) => actor.")
                    .Append(method.Name)
                    .Append("(request");
                if (method.HasCancellationToken)
                {
                    builder.Append(", ct");
                }

                builder.AppendLine("), cancellationToken);");
            }
            else
            {
                builder.Append(indent)
                    .Append("    return _runtime.AskAsync<")
                    .Append(actorType)
                    .Append(", ")
                    .Append(DisplayType(method.ResultType, actor.Symbol.ContainingNamespace))
                    .Append(">(actorId, (actor, ct) => actor.")
                    .Append(method.Name)
                    .Append("(request");
                if (method.HasCancellationToken)
                {
                    builder.Append(", ct");
                }

                builder.AppendLine("), cancellationToken);");
            }

            builder.Append(indent).AppendLine("}");
        }

        private static void AppendRemoteRef(
            StringBuilder builder,
            ActorInfo actor,
            string remoteRefType,
            string keyType,
            string routePrefix,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            builder.Append(indent).Append("public readonly struct ").Append(remoteRefType).AppendLine();
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IRemoteActorInvoker _remote;");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IRemoteActorSerializer _serializer;");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.RemoteActorOptions _options;");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Cluster.NodeId _node;");
            builder.Append(indent).Append("    private readonly ").Append(keyType).AppendLine(" _id;");
            builder.AppendLine();
            builder.Append(indent).Append("    public ").Append(remoteRefType).Append("(").AppendLine();
            builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.IRemoteActorInvoker remote,");
            builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.IRemoteActorSerializer serializer,");
            builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.RemoteActorOptions options,");
            builder.Append(indent).AppendLine("        global::ULinkGame.Cluster.NodeId nodeId,");
            builder.Append(indent).Append("        ").Append(keyType).AppendLine(" id)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        _remote = remote;");
            builder.Append(indent).AppendLine("        _serializer = serializer;");
            builder.Append(indent).AppendLine("        _options = options;");
            builder.Append(indent).AppendLine("        _node = nodeId;");
            builder.Append(indent).AppendLine("        _id = id;");
            builder.Append(indent).AppendLine("    }");

            foreach (var method in actor.Methods)
            {
                builder.AppendLine();
                AppendRemoteMethod(builder, actor, method, routePrefix, indentLevel + 1);
            }

            builder.Append(indent).AppendLine("}");
        }

        private static void AppendRemoteMethod(
            StringBuilder builder,
            ActorInfo actor,
            MethodInfo method,
            string routePrefix,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            var returnType = DisplayReturnType(actor, method);
            var requestType = DisplayType(method.RequestType, actor.Symbol.ContainingNamespace);
            var actorName = actor.ActorName;
            var methodName = method.ActorMethodName;

            builder.Append(indent)
                .Append("public async ")
                .Append(returnType)
                .Append(' ')
                .Append(method.Name)
                .Append('(')
                .Append(requestType)
                .Append(" request, global::System.Threading.CancellationToken cancellationToken = default)")
                .AppendLine();
            builder.Append(indent).AppendLine("{");
            AppendRemoteInvocationSetup(builder, actor, routePrefix, actorName, methodName, indentLevel + 1);

            if (method.ResultType == null)
            {
                builder.Append(indent).AppendLine("    var result = await _remote.TellAsync(invocation, cancellationToken).ConfigureAwait(false);");
                builder.Append(indent).AppendLine("    if (result.Status != global::ULinkGame.Server.Actors.RemoteActorStatus.Accepted)");
                builder.Append(indent).AppendLine("    {");
                AppendThrowRemoteActorException(builder, actorName, methodName, indentLevel + 2);
                builder.Append(indent).AppendLine("    }");
            }
            else
            {
                builder.Append(indent).AppendLine("    var result = await _remote.AskAsync(invocation, cancellationToken).ConfigureAwait(false);");
                builder.Append(indent).AppendLine("    if (result.Status != global::ULinkGame.Server.Actors.RemoteActorStatus.Replied)");
                builder.Append(indent).AppendLine("    {");
                AppendThrowRemoteActorException(builder, actorName, methodName, indentLevel + 2);
                builder.Append(indent).AppendLine("    }");
                builder.Append(indent)
                    .Append("    return _serializer.Deserialize<")
                    .Append(DisplayType(method.ResultType, actor.Symbol.ContainingNamespace))
                    .AppendLine(">(result.Payload);");
            }

            builder.Append(indent).AppendLine("}");
        }

        private static void AppendClusterHandler(
            StringBuilder builder,
            ActorInfo actor,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            var handlerType = actor.Symbol.Name + "ClusterHandler";

            builder.Append(indent).Append("public sealed class ").Append(handlerType).AppendLine(" : global::ULinkGame.Cluster.IClusterMessageHandler");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Server.Actors.IRemoteActorSerializer _serializer;");
            builder.Append(indent).AppendLine("    private readonly global::ULinkGame.Cluster.IClusterRouter _router;");
            builder.AppendLine();
            builder.Append(indent).Append("    public ").Append(handlerType).AppendLine("(");
            builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.IActorRuntime runtime,");
            builder.Append(indent).AppendLine("        global::ULinkGame.Server.Actors.IRemoteActorSerializer serializer,");
            builder.Append(indent).AppendLine("        global::ULinkGame.Cluster.IClusterRouter router)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        _runtime = runtime;");
            builder.Append(indent).AppendLine("        _serializer = serializer;");
            builder.Append(indent).AppendLine("        _router = router;");
            builder.Append(indent).AppendLine("    }");
            builder.AppendLine();
            builder.Append(indent).AppendLine("    public async global::System.Threading.Tasks.ValueTask<global::ULinkGame.Cluster.ClusterSendStatus> HandleAsync(");
            builder.Append(indent).AppendLine("        global::ULinkGame.Cluster.ClusterMessage message,");
            builder.Append(indent).AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        if (!global::ULinkGame.Cluster.ClusterActorEnvelope.TryFromClusterMessage(message, out var envelope) || envelope is null)");
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).AppendLine("            return global::ULinkGame.Cluster.ClusterSendStatus.RouteNotFound;");
            builder.Append(indent).AppendLine("        }");
            builder.AppendLine();
            builder.Append(indent)
                .Append("        if (!envelope.ActorId.StartsWith(\"")
                .Append(actor.ActorName)
                .Append("/\", global::System.StringComparison.Ordinal))")
                .AppendLine();
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).AppendLine("            return global::ULinkGame.Cluster.ClusterSendStatus.RouteNotFound;");
            builder.Append(indent).AppendLine("        }");
            builder.AppendLine();
            builder.Append(indent).AppendLine("        var actorId = global::ULinkGame.Server.Actors.ActorId.From(envelope.ActorId);");
            builder.Append(indent).AppendLine("        switch (envelope.Kind)");
            builder.Append(indent).AppendLine("        {");

            foreach (var method in actor.Methods)
            {
                AppendClusterHandlerCase(builder, actor, method, indentLevel + 3);
            }

            builder.Append(indent).AppendLine("            default:");
            builder.Append(indent).AppendLine("                return global::ULinkGame.Cluster.ClusterSendStatus.RouteNotFound;");
            builder.Append(indent).AppendLine("        }");
            builder.Append(indent).AppendLine("    }");
            builder.Append(indent).AppendLine("}");
        }

        private static void AppendClusterHandlerCase(
            StringBuilder builder,
            ActorInfo actor,
            MethodInfo method,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            var actorType = actor.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var requestType = DisplayType(method.RequestType, actor.Symbol.ContainingNamespace);

            builder.Append(indent).Append("case \"").Append(method.ActorMethodName).AppendLine("\":");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("    var request = _serializer.Deserialize<").Append(requestType).AppendLine(">(envelope.Payload);");

            if (method.ResultType == null)
            {
                builder.Append(indent)
                    .Append("    await _runtime.TellAsync<")
                    .Append(actorType)
                    .Append(">(actorId, (actor, ct) => actor.")
                    .Append(method.Name)
                    .Append("(request");
                if (method.HasCancellationToken)
                {
                    builder.Append(", ct");
                }

                builder.AppendLine("), cancellationToken).ConfigureAwait(false);");
            }
            else
            {
                builder.Append(indent)
                    .Append("    var reply = await _runtime.AskAsync<")
                    .Append(actorType)
                    .Append(", ")
                    .Append(DisplayType(method.ResultType, actor.Symbol.ContainingNamespace))
                    .Append(">(actorId, (actor, ct) => actor.")
                    .Append(method.Name)
                    .Append("(request");
                if (method.HasCancellationToken)
                {
                    builder.Append(", ct");
                }

                builder.AppendLine("), cancellationToken).ConfigureAwait(false);");
                builder.Append(indent).AppendLine("    if (envelope.ReplyCorrelationId is not null)");
                builder.Append(indent).AppendLine("    {");
                builder.Append(indent).AppendLine("        await global::ULinkGame.Server.Actors.RemoteActorGateway.SendReplyAsync(");
                builder.Append(indent).AppendLine("            _router,");
                builder.Append(indent).AppendLine("            envelope.SourceNode,");
                builder.Append(indent).AppendLine("            envelope.ReplyCorrelationId,");
                builder.Append(indent).AppendLine("            _serializer.Serialize(reply),");
                builder.Append(indent).AppendLine("            cancellationToken).ConfigureAwait(false);");
                builder.Append(indent).AppendLine("    }");
            }

            builder.Append(indent).AppendLine("    return global::ULinkGame.Cluster.ClusterSendStatus.Accepted;");
            builder.Append(indent).AppendLine("}");
        }

        private static void AppendRemoteInvocationSetup(
            StringBuilder builder,
            ActorInfo actor,
            string routePrefix,
            string actorName,
            string methodName,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            builder.Append(indent)
                .Append("var actorId = global::ULinkGame.Server.Actors.ActorId.From(\"")
                .Append(routePrefix)
                .Append("/\" + ")
                .Append(CreateKeyValueExpression(actor.KeyType))
                .AppendLine(");");
            builder.Append(indent).AppendLine("var payload = _serializer.Serialize(request);");
            builder.Append(indent).AppendLine("var correlationId = global::System.Guid.NewGuid().ToString(\"N\");");
            builder.Append(indent).AppendLine("var deadline = global::System.DateTimeOffset.UtcNow.Add(_options.DefaultTimeout);");
            builder.Append(indent)
                .Append("var invocation = new global::ULinkGame.Server.Actors.RemoteActorInvocation(_node, actorId, \"")
                .Append(actorName)
                .Append("\", \"")
                .Append(methodName)
                .AppendLine("\", payload, deadline, correlationId);");
        }

        private static void AppendThrowRemoteActorException(
            StringBuilder builder,
            string actorName,
            string methodName,
            int indentLevel)
        {
            var indent = Indent(indentLevel);
            builder.Append(indent).AppendLine("throw new global::ULinkGame.Server.Actors.RemoteActorException(");
            builder.Append(indent).AppendLine("    result.Status,");
            builder.Append(indent).AppendLine("    actorId,");
            builder.Append(indent).Append("    \"").Append(actorName).AppendLine("\",");
            builder.Append(indent).Append("    \"").Append(methodName).AppendLine("\",");
            builder.Append(indent).AppendLine("    result.Message ?? string.Empty,");
            builder.Append(indent).AppendLine("    _node,");
            builder.Append(indent).AppendLine("    correlationId);");
        }

        private static string DisplayReturnType(ActorInfo actor, MethodInfo method)
        {
            if (method.ResultType == null)
            {
                return "global::System.Threading.Tasks.ValueTask";
            }

            return "global::System.Threading.Tasks.ValueTask<" + DisplayType(method.ResultType, actor.Symbol.ContainingNamespace) + ">";
        }

        private static string DisplayType(ITypeSymbol type, INamespaceSymbol actorNamespace)
        {
            if (SymbolEqualityComparer.Default.Equals(type.ContainingNamespace, actorNamespace))
            {
                return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }

            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string CreateKeyValueExpression(ITypeSymbol keyType)
        {
            if (keyType.SpecialType == SpecialType.System_String)
            {
                return "_id";
            }

            if (HasAccessibleValueProperty(keyType))
            {
                return "_id.Value";
            }

            return "_id.ToString()";
        }

        private static bool HasAccessibleValueProperty(ITypeSymbol keyType)
        {
            return keyType.GetMembers("Value")
                .OfType<IPropertySymbol>()
                .Any(static property =>
                    !property.IsStatic &&
                    property.GetMethod != null &&
                    IsAccessiblePropertyGetter(property.GetMethod.DeclaredAccessibility));
        }

        private static bool IsAccessiblePropertyGetter(Accessibility accessibility)
        {
            return accessibility == Accessibility.Public ||
                accessibility == Accessibility.Internal ||
                accessibility == Accessibility.ProtectedOrInternal;
        }

        private static string GetRemoteMethodName(string methodName)
        {
            var normalized = methodName.EndsWith("Async", System.StringComparison.Ordinal) && methodName.Length > "Async".Length
                ? methodName.Substring(0, methodName.Length - "Async".Length)
                : methodName;

            return LowerFirst(normalized);
        }

        private static string GetActorPrefix(string actorName)
        {
            return actorName.EndsWith("Actor", System.StringComparison.Ordinal) && actorName.Length > "Actor".Length
                ? actorName.Substring(0, actorName.Length - "Actor".Length)
                : actorName;
        }

        private static string LowerFirst(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static string Indent(int indentLevel)
        {
            return new string(' ', indentLevel * 4);
        }

        private static string CreateHintName(INamedTypeSymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty)
                .Replace('.', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace(',', '_')
                .Replace(' ', '_') + ".Actors.g.cs";
        }

        private sealed class ActorInfo
        {
            public ActorInfo(
                INamedTypeSymbol symbol,
                ITypeSymbol keyType,
                string actorName,
                bool isLocalOnly,
                MethodInfo[] methods,
                UnsupportedMethodInfo[] unsupportedMethods)
            {
                Symbol = symbol;
                KeyType = keyType;
                ActorName = actorName;
                IsLocalOnly = isLocalOnly;
                Methods = methods;
                UnsupportedMethods = unsupportedMethods;
            }

            public INamedTypeSymbol Symbol { get; }

            public ITypeSymbol KeyType { get; }

            public string ActorName { get; }

            public bool IsLocalOnly { get; }

            public MethodInfo[] Methods { get; }

            public UnsupportedMethodInfo[] UnsupportedMethods { get; }
        }

        private sealed class MethodInfo
        {
            private MethodInfo(
                string name,
                string actorMethodName,
                ITypeSymbol requestType,
                ITypeSymbol? resultType,
                bool hasCancellationToken)
            {
                Name = name;
                ActorMethodName = actorMethodName;
                RequestType = requestType;
                ResultType = resultType;
                HasCancellationToken = hasCancellationToken;
            }

            public string Name { get; }

            public string ActorMethodName { get; }

            public ITypeSymbol RequestType { get; }

            public ITypeSymbol? ResultType { get; }

            public bool HasCancellationToken { get; }

            public static MethodInfo Create(IMethodSymbol method)
            {
                IsValueTask(method.ReturnType, out var resultType);
                return new MethodInfo(
                    method.Name,
                    GetAttributeString(method, ActorMethodAttributeName) ?? GetRemoteMethodName(method.Name),
                    method.Parameters[0].Type,
                    resultType,
                    method.Parameters.Length == 2);
            }
        }

        private sealed class UnsupportedMethodInfo
        {
            public UnsupportedMethodInfo(string name, Location location)
            {
                Name = name;
                Location = location;
            }

            public string Name { get; }

            public Location Location { get; }
        }
    }
}
