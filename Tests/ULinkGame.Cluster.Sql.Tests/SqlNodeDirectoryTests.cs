using System.Data.Common;
using Microsoft.Data.Sqlite;
using ULinkGame.Cluster;
using ULinkGame.Cluster.Sql;
using Xunit;

namespace ULinkGame.Cluster.Sql.Tests;

public sealed class SqlNodeDirectoryTests
{
    [Fact]
    public async Task RegisterPersistsAndIncrementsEpochAcrossDirectoryInstances()
    {
        await using var connection = await OpenConnectionAsync();
        await SqlNodeDirectorySchema.EnsureCreatedAsync(
            connection,
            SqlNodeDirectoryDialect.Sqlite,
            cancellationToken: TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var first = CreateDirectory(connection);

        var firstResult = await first.RegisterAsync(
            TestRegistration("local", "node-a", now),
            now,
            TestContext.Current.CancellationToken);
        var second = CreateDirectory(connection);
        var secondResult = await second.RegisterAsync(
            TestRegistration("local", "node-a", now.AddSeconds(1)),
            now.AddSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(NodeRegistrationStatus.Registered, firstResult.Status);
        Assert.Equal(NodeRegistrationStatus.Registered, secondResult.Status);
        Assert.Equal(1, firstResult.Record!.NodeEpoch);
        Assert.Equal(2, secondResult.Record!.NodeEpoch);
    }

    [Fact]
    public async Task HeartbeatRejectsMismatchedEpoch()
    {
        await using var connection = await OpenConnectionAsync();
        await SqlNodeDirectorySchema.EnsureCreatedAsync(
            connection,
            SqlNodeDirectoryDialect.Sqlite,
            cancellationToken: TestContext.Current.CancellationToken);
        var directory = CreateDirectory(connection);
        var now = DateTimeOffset.UtcNow;
        await directory.RegisterAsync(
            TestRegistration("local", "node-a", now),
            now,
            TestContext.Current.CancellationToken);
        await directory.RegisterAsync(
            TestRegistration("local", "node-a", now.AddSeconds(1)),
            now.AddSeconds(1),
            TestContext.Current.CancellationToken);

        var status = await directory.HeartbeatAsync(
            "local",
            "node-a",
            1,
            now.AddSeconds(40),
            now.AddSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(NodeHeartbeatStatus.EpochMismatch, status);
    }

    [Fact]
    public async Task QueryFiltersByPersistedServiceKind()
    {
        await using var connection = await OpenConnectionAsync();
        await SqlNodeDirectorySchema.EnsureCreatedAsync(
            connection,
            SqlNodeDirectoryDialect.Sqlite,
            cancellationToken: TestContext.Current.CancellationToken);
        var directory = CreateDirectory(connection);
        var now = DateTimeOffset.UtcNow;
        await directory.RegisterAsync(
            TestRegistration("local", "gateway-1", now, "gateway"),
            now,
            TestContext.Current.CancellationToken);
        await directory.RegisterAsync(
            TestRegistration("local", "room-1", now, "room"),
            now,
            TestContext.Current.CancellationToken);

        var rooms = await directory.QueryAsync(
            new NodeDirectoryQuery("local", serviceKind: "room"),
            now,
            TestContext.Current.CancellationToken);

        Assert.Single(rooms);
        Assert.Equal("room-1", rooms[0].NodeId.Value);
    }

    [Fact]
    public async Task UpdateStatePersistsAndResolveReturnsUpdatedRecord()
    {
        await using var connection = await OpenConnectionAsync();
        await SqlNodeDirectorySchema.EnsureCreatedAsync(
            connection,
            SqlNodeDirectoryDialect.Sqlite,
            cancellationToken: TestContext.Current.CancellationToken);
        var directory = CreateDirectory(connection);
        var now = DateTimeOffset.UtcNow;
        var registered = await directory.RegisterAsync(
            TestRegistration("local", "node-a", now),
            now,
            TestContext.Current.CancellationToken);

        var status = await directory.UpdateStateAsync(
            "local",
            "node-a",
            registered.Record!.NodeEpoch,
            NodeState.Draining,
            now.AddSeconds(1),
            TestContext.Current.CancellationToken);
        var resolved = await directory.ResolveAsync(
            "local",
            "node-a",
            now.AddSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(NodeStateUpdateStatus.Updated, status);
        Assert.NotNull(resolved);
        Assert.Equal(NodeState.Draining, resolved!.State);
    }

    [Fact]
    public async Task ResolveReturnsNullForDeadNode()
    {
        await using var connection = await OpenConnectionAsync();
        await SqlNodeDirectorySchema.EnsureCreatedAsync(
            connection,
            SqlNodeDirectoryDialect.Sqlite,
            cancellationToken: TestContext.Current.CancellationToken);
        var directory = CreateDirectory(connection);
        var now = DateTimeOffset.UtcNow;
        var registered = await directory.RegisterAsync(
            TestRegistration("local", "node-a", now),
            now,
            TestContext.Current.CancellationToken);
        await directory.UpdateStateAsync(
            "local",
            "node-a",
            registered.Record!.NodeEpoch,
            NodeState.Dead,
            now.AddSeconds(1),
            TestContext.Current.CancellationToken);

        var resolved = await directory.ResolveAsync(
            "local",
            "node-a",
            now.AddSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ExpireMarksExpiredNodesDead()
    {
        await using var connection = await OpenConnectionAsync();
        await SqlNodeDirectorySchema.EnsureCreatedAsync(
            connection,
            SqlNodeDirectoryDialect.Sqlite,
            cancellationToken: TestContext.Current.CancellationToken);
        var directory = CreateDirectory(connection);
        var now = DateTimeOffset.UtcNow;
        await directory.RegisterAsync(
            TestRegistration("local", "node-a", now),
            now,
            TestContext.Current.CancellationToken);

        var expired = await directory.ExpireAsync(
            "local",
            now.AddMinutes(1),
            TestContext.Current.CancellationToken);
        var records = await directory.QueryAsync(
            new NodeDirectoryQuery("local", state: NodeState.Dead, includeExpired: true),
            now.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, expired);
        Assert.Single(records);
        Assert.Equal("node-a", records[0].NodeId.Value);
        Assert.Equal(NodeState.Dead, records[0].State);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static SqlNodeDirectory CreateDirectory(DbConnection connection)
    {
        return new SqlNodeDirectory(
            new SqlNodeDirectoryOptions(
                () => ValueTask.FromResult(connection),
                SqlNodeDirectoryDialect.Sqlite));
    }

    private static NodeRegistration TestRegistration(
        string clusterName,
        string nodeId,
        DateTimeOffset now,
        string serviceKind = "gateway")
    {
        return new NodeRegistration(
            clusterName,
            nodeId,
            new Dictionary<string, NodeEndpoint>
            {
                ["cluster"] = new NodeEndpoint(
                    $"tcp://127.0.0.1:{21000 + Math.Abs(nodeId.GetHashCode() % 1000)}",
                    new Dictionary<string, string>
                    {
                        ["transport"] = "tcp"
                    })
            },
            new[]
            {
                new NodeServiceDescriptor(
                    serviceKind,
                    metadata: new Dictionary<string, string>
                    {
                        ["role"] = serviceKind
                    })
            },
            now.AddSeconds(30),
            NodeState.Ready,
            new Dictionary<string, string>
            {
                ["zone"] = "local"
            });
    }
}
