using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ULinkGame.Cluster
{
    public sealed class RouteLocation
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

        public RouteLocation(
            RouteKey route,
            NodeId node,
            NodeEndpoint endpoint,
            DateTimeOffset expiresAt,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            Route = route;
            Node = node;
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ExpiresAt = expiresAt;
            Metadata = metadata is null
                ? EmptyMetadata
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(metadata, StringComparer.Ordinal));
        }

        public RouteKey Route { get; }

        public NodeId Node { get; }

        public NodeEndpoint Endpoint { get; }

        public DateTimeOffset ExpiresAt { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        public bool IsExpired(DateTimeOffset now)
        {
            return now >= ExpiresAt;
        }
    }
}
