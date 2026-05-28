using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ULinkGame.Cluster
{
    public static class ClusterDiagnostics
    {
        public const string MeterName = "ULinkGame.Cluster";
        public const string ActivitySourceName = "ULinkGame.Cluster";
        public const string NodeDirectoryRegistrationMetricName = "ulinkgame.cluster.node_directory.registration";
        public const string NodeDirectoryHeartbeatMetricName = "ulinkgame.cluster.node_directory.heartbeat";
        public const string NodeDirectoryExpiredMetricName = "ulinkgame.cluster.node_directory.expired";

        public static readonly Meter Meter = new Meter(MeterName, "0.1.1");
        public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, "0.1.1");

        internal static readonly Counter<long> RouteLookupCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.lookup");

        internal static readonly Counter<long> SendCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.sent");

        internal static readonly Counter<long> ReceiveCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.received");

        internal static readonly Counter<long> DispatchCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.dispatched");

        internal static readonly Counter<long> DropCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.dropped");

        internal static readonly Counter<long> ExpiredCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.expired");

        internal static readonly Counter<long> BackpressureCounter = Meter.CreateCounter<long>(
            "ulinkgame.cluster.route.backpressure");

        internal static Activity? StartActivity(string name, ClusterMessage message)
        {
            var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("messaging.system", "ulinkgame.cluster");
                activity.SetTag("messaging.operation", name);
                activity.SetTag("ulinkgame.cluster.message.kind", message.Kind);
                activity.SetTag("ulinkgame.cluster.correlation.present", message.CorrelationId is not null);
                activity.SetTag("ulinkgame.cluster.trace.present", message.TraceId is not null);
            }

            return activity;
        }

        internal static void AddRouteLookup(string status, string kind)
        {
            RouteLookupCounter.Add(1, Tags("lookup", status, null, kind));
        }

        internal static void AddSend(string status, string delivery, string kind)
        {
            SendCounter.Add(1, Tags("send", status, delivery, kind));
        }

        internal static void AddReceive(string status, string kind)
        {
            ReceiveCounter.Add(1, Tags("receive", status, "remote", kind));
        }

        internal static void AddDispatch(string status, string delivery, string kind)
        {
            DispatchCounter.Add(1, Tags("dispatch", status, delivery, kind));
        }

        internal static void AddDrop(string status, string kind)
        {
            DropCounter.Add(1, Tags("drop", status, null, kind));
        }

        internal static void AddExpired(string kind)
        {
            ExpiredCounter.Add(1, Tags("expire", "expired", null, kind));
        }

        internal static void AddBackpressure(string stage, string delivery, string kind)
        {
            BackpressureCounter.Add(1, Tags(stage, "backpressure", delivery, kind));
        }

        internal static string StatusTag(ClusterSendStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        private static TagList Tags(string stage, string status, string? delivery, string kind)
        {
            var tags = new TagList
            {
                { "ulinkgame.cluster.stage", stage },
                { "ulinkgame.cluster.status", status },
                { "ulinkgame.cluster.message.kind", kind }
            };

            if (delivery is not null)
            {
                tags.Add("ulinkgame.cluster.delivery", delivery);
            }

            return tags;
        }
    }
}
