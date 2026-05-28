# Node Directory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add node-directory support with node-local service descriptors, lease/heartbeat semantics, first-version persistence, ULinkRPC remote access, and configuration-oriented samples.

**Architecture:** `ULinkGame.Cluster` owns transport-neutral node-directory contracts and an in-memory implementation. A new `ULinkGame.Cluster.Sql` package owns production-oriented persistent storage through caller-provided `DbConnection` factories and dialect-specific SQL. `ULinkGame.Cluster.ULinkRPC` exposes node-directory calls over ULinkRPC, matching the existing route-directory client/binder pattern.

**Tech Stack:** C#/.NET, xUnit v3, ULinkRPC, `System.Data.Common`, `System.Text.Json`, `Microsoft.Data.Sqlite` for persistent adapter tests.

---

## File Structure

- Create `src/ULinkGame.Cluster/Nodes/NodeState.cs`
  - Node lifecycle enum: `Starting`, `Ready`, `Draining`, `Suspect`, `Dead`.
- Create `src/ULinkGame.Cluster/Nodes/NodeServiceDescriptor.cs`
  - Node-local service descriptor: kind, name, metadata.
- Create `src/ULinkGame.Cluster/Nodes/NodeRegistration.cs`
  - Registration request: cluster name, node id, endpoints, services, labels, state, lease expiration.
- Create `src/ULinkGame.Cluster/Nodes/NodeRecord.cs`
  - Directory record returned to callers, including assigned node epoch.
- Create `src/ULinkGame.Cluster/Nodes/NodeDirectoryQuery.cs`
  - Query filter for service kind/name, label, state, and expired inclusion.
- Create `src/ULinkGame.Cluster/Nodes/NodeRegistrationResult.cs`
  - Registration status plus assigned node record.
- Create `src/ULinkGame.Cluster/Nodes/NodeRegistrationStatus.cs`
  - `Registered`, `InvalidRegistration`.
- Create `src/ULinkGame.Cluster/Nodes/NodeHeartbeatStatus.cs`
  - `Refreshed`, `NodeNotFound`, `EpochMismatch`, `Expired`.
- Create `src/ULinkGame.Cluster/Nodes/NodeStateUpdateStatus.cs`
  - `Updated`, `NodeNotFound`, `EpochMismatch`, `Expired`.
- Create `src/ULinkGame.Cluster/Nodes/INodeDirectory.cs`
  - Transport-neutral node directory contract.
- Create `src/ULinkGame.Cluster/Nodes/InMemoryNodeDirectory.cs`
  - Thread-safe in-memory directory for tests/local development.
- Create `Tests/ULinkGame.Cluster.Tests/InMemoryNodeDirectoryTests.cs`
  - Unit tests for registration, epoch allocation, heartbeat, query, state update, expiration.
- Create `src/ULinkGame.Cluster.Sql/ULinkGame.Cluster.Sql.csproj`
  - New persistent adapter package.
- Create `src/ULinkGame.Cluster.Sql/SqlNodeDirectory.cs`
  - Persistent `INodeDirectory` backed by `DbConnection`.
- Create `src/ULinkGame.Cluster.Sql/SqlNodeDirectoryOptions.cs`
  - Connection factory, dialect, table name, cluster name default.
- Create `src/ULinkGame.Cluster.Sql/SqlNodeDirectoryDialect.cs`
  - `Sqlite`, `Postgres`, `MySql`.
- Create `src/ULinkGame.Cluster.Sql/SqlNodeDirectorySchema.cs`
  - Schema creation SQL for tests and generated projects.
- Create `Tests/ULinkGame.Cluster.Sql.Tests/ULinkGame.Cluster.Sql.Tests.csproj`
  - Tests using `Microsoft.Data.Sqlite`.
- Create `Tests/ULinkGame.Cluster.Sql.Tests/SqlNodeDirectoryTests.cs`
  - Persistent behavior tests.
- Create `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectoryMessages.cs`
  - DTOs for node registration, heartbeat, resolve, query, state update, expire.
- Create `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectoryRecordConverter.cs`
  - DTO conversion.
- Create `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectory.cs`
  - Remote `INodeDirectory` client.
- Create `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectoryBinder.cs`
  - Server-side binder.
- Modify `src/ULinkGame.Cluster.ULinkRPC/Protocol/ULinkRpcClusterProtocol.cs`
  - Add node-directory method IDs.
- Create `Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkRpcNodeDirectoryTests.cs`
  - Client/binder tests.
- Modify `Tests/tests.slnx`
  - Add `ULinkGame.Cluster.Sql.Tests`.
- Modify `CONTRIBUTING.md`, `src/ULinkGame.Cluster/README.md`, `src/ULinkGame.Cluster.ULinkRPC/README.md`, `src/ULinkGame.Tool/README.md`
  - Keep docs aligned with final APIs and storage options.

---

### Task 1: Core Node Directory Models

**Files:**
- Create: `src/ULinkGame.Cluster/Nodes/NodeState.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeServiceDescriptor.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeRegistration.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeRecord.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeDirectoryQuery.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeRegistrationResult.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeRegistrationStatus.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeHeartbeatStatus.cs`
- Create: `src/ULinkGame.Cluster/Nodes/NodeStateUpdateStatus.cs`
- Create: `src/ULinkGame.Cluster/Nodes/INodeDirectory.cs`
- Test: `Tests/ULinkGame.Cluster.Tests/NodeDirectoryModelTests.cs`

- [ ] **Step 1: Write model validation tests**

Add `Tests/ULinkGame.Cluster.Tests/NodeDirectoryModelTests.cs`:

```csharp
using ULinkGame.Cluster;

namespace ULinkGame.Cluster.Tests;

public sealed class NodeDirectoryModelTests
{
    [Fact]
    public void ServiceDescriptorRequiresKind()
    {
        Assert.Throws<ArgumentException>(() => new NodeServiceDescriptor("", "gateway"));
    }

    [Fact]
    public void ServiceDescriptorDefaultsNameToKind()
    {
        var descriptor = new NodeServiceDescriptor("gateway");

        Assert.Equal("gateway", descriptor.Kind);
        Assert.Equal("gateway", descriptor.Name);
    }

    [Fact]
    public void RegistrationRequiresAtLeastOneService()
    {
        Assert.Throws<ArgumentException>(() => new NodeRegistration(
            "local",
            "node-a",
            new Dictionary<string, NodeEndpoint>
            {
                ["cluster"] = new NodeEndpoint("tcp://127.0.0.1:21000")
            },
            Array.Empty<NodeServiceDescriptor>(),
            DateTimeOffset.UtcNow.AddSeconds(30)));
    }

    [Fact]
    public void RecordRejectsNegativeEpoch()
    {
        var registration = TestRegistration();

        Assert.Throws<ArgumentOutOfRangeException>(() => new NodeRecord(
            registration.ClusterName,
            registration.NodeId,
            -1,
            registration.Endpoints,
            registration.Services,
            registration.Labels,
            NodeState.Ready,
            DateTimeOffset.UtcNow.AddSeconds(30),
            DateTimeOffset.UtcNow));
    }

    private static NodeRegistration TestRegistration()
    {
        return new NodeRegistration(
            "local",
            "node-a",
            new Dictionary<string, NodeEndpoint>
            {
                ["cluster"] = new NodeEndpoint("tcp://127.0.0.1:21000")
            },
            new[]
            {
                new NodeServiceDescriptor("gateway")
            },
            DateTimeOffset.UtcNow.AddSeconds(30));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Tests/ULinkGame.Cluster.Tests.csproj --filter NodeDirectoryModelTests
```

Expected: compile fails because node-directory model types do not exist.

- [ ] **Step 3: Add model and interface files**

Implement the files listed in this task with these public shapes:

```csharp
namespace ULinkGame.Cluster
{
    public enum NodeState
    {
        Starting = 0,
        Ready = 1,
        Draining = 2,
        Suspect = 3,
        Dead = 4
    }
}
```

```csharp
namespace ULinkGame.Cluster
{
    public sealed class NodeServiceDescriptor
    {
        public NodeServiceDescriptor(
            string kind,
            string? name = null,
            IReadOnlyDictionary<string, string>? metadata = null);

        public string Kind { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }
}
```

```csharp
namespace ULinkGame.Cluster
{
    public sealed class NodeRegistration
    {
        public NodeRegistration(
            string clusterName,
            NodeId nodeId,
            IReadOnlyDictionary<string, NodeEndpoint> endpoints,
            IReadOnlyList<NodeServiceDescriptor> services,
            DateTimeOffset leaseExpiresAt,
            NodeState state = NodeState.Starting,
            IReadOnlyDictionary<string, string>? labels = null);

        public string ClusterName { get; }
        public NodeId NodeId { get; }
        public IReadOnlyDictionary<string, NodeEndpoint> Endpoints { get; }
        public IReadOnlyList<NodeServiceDescriptor> Services { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public NodeState State { get; }
        public DateTimeOffset LeaseExpiresAt { get; }
    }
}
```

```csharp
namespace ULinkGame.Cluster
{
    public sealed class NodeRecord
    {
        public NodeRecord(
            string clusterName,
            NodeId nodeId,
            long nodeEpoch,
            IReadOnlyDictionary<string, NodeEndpoint> endpoints,
            IReadOnlyList<NodeServiceDescriptor> services,
            IReadOnlyDictionary<string, string>? labels,
            NodeState state,
            DateTimeOffset leaseExpiresAt,
            DateTimeOffset updatedAt);

        public string ClusterName { get; }
        public NodeId NodeId { get; }
        public long NodeEpoch { get; }
        public IReadOnlyDictionary<string, NodeEndpoint> Endpoints { get; }
        public IReadOnlyList<NodeServiceDescriptor> Services { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public NodeState State { get; }
        public DateTimeOffset LeaseExpiresAt { get; }
        public DateTimeOffset UpdatedAt { get; }
        public bool IsExpired(DateTimeOffset now);
        public bool HasService(string kind, string? name = null);
    }
}
```

```csharp
namespace ULinkGame.Cluster
{
    public sealed class NodeDirectoryQuery
    {
        public NodeDirectoryQuery(
            string clusterName,
            string? serviceKind = null,
            string? serviceName = null,
            NodeState? state = null,
            IReadOnlyDictionary<string, string>? labels = null,
            bool includeExpired = false);

        public string ClusterName { get; }
        public string? ServiceKind { get; }
        public string? ServiceName { get; }
        public NodeState? State { get; }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public bool IncludeExpired { get; }
    }
}
```

```csharp
namespace ULinkGame.Cluster
{
    public enum NodeRegistrationStatus
    {
        Registered = 0,
        InvalidRegistration = 1
    }

    public sealed class NodeRegistrationResult
    {
        public NodeRegistrationResult(NodeRegistrationStatus status, NodeRecord? record);
        public NodeRegistrationStatus Status { get; }
        public NodeRecord? Record { get; }
    }

    public enum NodeHeartbeatStatus
    {
        Refreshed = 0,
        NodeNotFound = 1,
        EpochMismatch = 2,
        Expired = 3
    }

    public enum NodeStateUpdateStatus
    {
        Updated = 0,
        NodeNotFound = 1,
        EpochMismatch = 2,
        Expired = 3
    }
}
```

```csharp
namespace ULinkGame.Cluster
{
    public interface INodeDirectory
    {
        ValueTask<NodeRegistrationResult> RegisterAsync(NodeRegistration registration, DateTimeOffset now, CancellationToken cancellationToken = default);
        ValueTask<NodeHeartbeatStatus> HeartbeatAsync(string clusterName, NodeId node, long nodeEpoch, DateTimeOffset leaseExpiresAt, DateTimeOffset now, CancellationToken cancellationToken = default);
        ValueTask<NodeStateUpdateStatus> UpdateStateAsync(string clusterName, NodeId node, long nodeEpoch, NodeState state, DateTimeOffset now, CancellationToken cancellationToken = default);
        ValueTask<NodeRecord?> ResolveAsync(string clusterName, NodeId node, DateTimeOffset now, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyList<NodeRecord>> QueryAsync(NodeDirectoryQuery query, DateTimeOffset now, CancellationToken cancellationToken = default);
        ValueTask<int> ExpireAsync(string clusterName, DateTimeOffset now, CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Tests/ULinkGame.Cluster.Tests.csproj --filter NodeDirectoryModelTests
```

Expected: tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ULinkGame.Cluster/Nodes Tests/ULinkGame.Cluster.Tests/NodeDirectoryModelTests.cs
git commit -m "Add node directory core models"
```

---

### Task 2: In-Memory Node Directory

**Files:**
- Create: `src/ULinkGame.Cluster/Nodes/InMemoryNodeDirectory.cs`
- Create: `Tests/ULinkGame.Cluster.Tests/InMemoryNodeDirectoryTests.cs`

- [ ] **Step 1: Write in-memory behavior tests**

Add tests covering:

```csharp
[Fact]
public async Task RegisterAssignsEpochOneForNewNode()
{
    var directory = new InMemoryNodeDirectory();
    var now = DateTimeOffset.UtcNow;

    var result = await directory.RegisterAsync(TestRegistration("local", "node-a", now), now);

    Assert.Equal(NodeRegistrationStatus.Registered, result.Status);
    Assert.NotNull(result.Record);
    Assert.Equal(1, result.Record!.NodeEpoch);
}
```

```csharp
[Fact]
public async Task RegisterIncrementsEpochForRestartedNode()
{
    var directory = new InMemoryNodeDirectory();
    var now = DateTimeOffset.UtcNow;

    var first = await directory.RegisterAsync(TestRegistration("local", "node-a", now), now);
    var second = await directory.RegisterAsync(TestRegistration("local", "node-a", now.AddSeconds(1)), now.AddSeconds(1));

    Assert.Equal(1, first.Record!.NodeEpoch);
    Assert.Equal(2, second.Record!.NodeEpoch);
}
```

```csharp
[Fact]
public async Task HeartbeatRejectsOldEpoch()
{
    var directory = new InMemoryNodeDirectory();
    var now = DateTimeOffset.UtcNow;
    await directory.RegisterAsync(TestRegistration("local", "node-a", now), now);
    await directory.RegisterAsync(TestRegistration("local", "node-a", now.AddSeconds(1)), now.AddSeconds(1));

    var status = await directory.HeartbeatAsync("local", "node-a", 1, now.AddSeconds(40), now.AddSeconds(2));

    Assert.Equal(NodeHeartbeatStatus.EpochMismatch, status);
}
```

```csharp
[Fact]
public async Task QueryFiltersByServiceKind()
{
    var directory = new InMemoryNodeDirectory();
    var now = DateTimeOffset.UtcNow;
    await directory.RegisterAsync(TestRegistration("local", "gateway-1", now, "gateway"), now);
    await directory.RegisterAsync(TestRegistration("local", "room-1", now, "room"), now);

    var records = await directory.QueryAsync(new NodeDirectoryQuery("local", serviceKind: "room"), now);

    Assert.Single(records);
    Assert.Equal("room-1", records[0].NodeId.Value);
}
```

```csharp
[Fact]
public async Task ExpireMarksExpiredNodesDead()
{
    var directory = new InMemoryNodeDirectory();
    var now = DateTimeOffset.UtcNow;
    await directory.RegisterAsync(TestRegistration("local", "node-a", now), now);

    var removed = await directory.ExpireAsync("local", now.AddMinutes(1));
    var record = await directory.ResolveAsync("local", "node-a", now.AddMinutes(1));

    Assert.Equal(1, removed);
    Assert.Null(record);
}
```

Include this helper in the test file:

```csharp
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
            ["cluster"] = new NodeEndpoint($"tcp://127.0.0.1:{21000 + Math.Abs(nodeId.GetHashCode() % 1000)}")
        },
        new[]
        {
            new NodeServiceDescriptor(serviceKind)
        },
        now.AddSeconds(30),
        NodeState.Ready);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Tests/ULinkGame.Cluster.Tests.csproj --filter InMemoryNodeDirectoryTests
```

Expected: compile fails because `InMemoryNodeDirectory` does not exist.

- [ ] **Step 3: Implement `InMemoryNodeDirectory`**

Implementation requirements:

- Use `lock` plus `Dictionary<(string ClusterName, NodeId NodeId), NodeRecord>`.
- `RegisterAsync` increments existing record epoch by 1, or assigns epoch 1.
- `HeartbeatAsync` requires matching cluster, node, and epoch.
- `HeartbeatAsync` returns `Expired` if current record is expired at `now`.
- `UpdateStateAsync` uses the same epoch and expiration checks as heartbeat.
- `ResolveAsync` returns `null` for expired records.
- `QueryAsync` filters expired records unless `IncludeExpired` is true.
- `ExpireAsync` marks matching expired records as `Dead` and returns the count.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Tests/ULinkGame.Cluster.Tests.csproj --filter InMemoryNodeDirectoryTests
```

Expected: tests pass.

- [ ] **Step 5: Run cluster test project**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Tests/ULinkGame.Cluster.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ULinkGame.Cluster/Nodes/InMemoryNodeDirectory.cs Tests/ULinkGame.Cluster.Tests/InMemoryNodeDirectoryTests.cs
git commit -m "Add in-memory node directory"
```

---

### Task 3: SQL Persistent Node Directory Package

**Files:**
- Create: `src/ULinkGame.Cluster.Sql/ULinkGame.Cluster.Sql.csproj`
- Create: `src/ULinkGame.Cluster.Sql/SqlNodeDirectoryDialect.cs`
- Create: `src/ULinkGame.Cluster.Sql/SqlNodeDirectoryOptions.cs`
- Create: `src/ULinkGame.Cluster.Sql/SqlNodeDirectorySchema.cs`
- Create: `src/ULinkGame.Cluster.Sql/SqlNodeDirectory.cs`
- Create: `Tests/ULinkGame.Cluster.Sql.Tests/ULinkGame.Cluster.Sql.Tests.csproj`
- Create: `Tests/ULinkGame.Cluster.Sql.Tests/SqlNodeDirectoryTests.cs`
- Modify: `Tests/tests.slnx`

- [ ] **Step 1: Add test project**

Create `Tests/ULinkGame.Cluster.Sql.Tests/ULinkGame.Cluster.Sql.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit.v3" Version="3.2.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/ULinkGame.Cluster.Sql/ULinkGame.Cluster.Sql.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write persistent behavior tests**

Add tests:

```csharp
[Fact]
public async Task RegisterPersistsAndIncrementsEpochAcrossDirectoryInstances()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    await SqlNodeDirectorySchema.EnsureCreatedAsync(connection, SqlNodeDirectoryDialect.Sqlite);

    var first = new SqlNodeDirectory(new SqlNodeDirectoryOptions(
        () => ValueTask.FromResult<DbConnection>(connection),
        SqlNodeDirectoryDialect.Sqlite));
    var now = DateTimeOffset.UtcNow;

    var firstResult = await first.RegisterAsync(TestRegistration("local", "node-a", now), now);
    var second = new SqlNodeDirectory(new SqlNodeDirectoryOptions(
        () => ValueTask.FromResult<DbConnection>(connection),
        SqlNodeDirectoryDialect.Sqlite));
    var secondResult = await second.RegisterAsync(TestRegistration("local", "node-a", now.AddSeconds(1)), now.AddSeconds(1));

    Assert.Equal(1, firstResult.Record!.NodeEpoch);
    Assert.Equal(2, secondResult.Record!.NodeEpoch);
}
```

```csharp
[Fact]
public async Task HeartbeatRejectsMismatchedEpoch()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    await SqlNodeDirectorySchema.EnsureCreatedAsync(connection, SqlNodeDirectoryDialect.Sqlite);
    var directory = CreateDirectory(connection);
    var now = DateTimeOffset.UtcNow;
    await directory.RegisterAsync(TestRegistration("local", "node-a", now), now);
    await directory.RegisterAsync(TestRegistration("local", "node-a", now.AddSeconds(1)), now.AddSeconds(1));

    var status = await directory.HeartbeatAsync("local", "node-a", 1, now.AddSeconds(40), now.AddSeconds(2));

    Assert.Equal(NodeHeartbeatStatus.EpochMismatch, status);
}
```

```csharp
[Fact]
public async Task QueryFiltersByPersistedServiceKind()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    await SqlNodeDirectorySchema.EnsureCreatedAsync(connection, SqlNodeDirectoryDialect.Sqlite);
    var directory = CreateDirectory(connection);
    var now = DateTimeOffset.UtcNow;
    await directory.RegisterAsync(TestRegistration("local", "gateway-1", now, "gateway"), now);
    await directory.RegisterAsync(TestRegistration("local", "room-1", now, "room"), now);

    var rooms = await directory.QueryAsync(new NodeDirectoryQuery("local", serviceKind: "room"), now);

    Assert.Single(rooms);
    Assert.Equal("room-1", rooms[0].NodeId.Value);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Sql.Tests/ULinkGame.Cluster.Sql.Tests.csproj
```

Expected: compile fails because the SQL project does not exist.

- [ ] **Step 4: Add SQL package project**

Create `src/ULinkGame.Cluster.Sql/ULinkGame.Cluster.Sql.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>ULinkGame.Cluster.Sql</RootNamespace>
    <PackageId>ULinkGame.Cluster.Sql</PackageId>
    <Version>0.1.0</Version>
    <Description>SQL-backed node directory persistence for ULinkGame cluster membership.</Description>
    <PackageTags>ulinkgame;game;cluster;node-directory;sql</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ULinkGame.Cluster\ULinkGame.Cluster.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Implement SQL adapter**

Implementation requirements:

- `SqlNodeDirectoryOptions` takes `Func<ValueTask<DbConnection>> ConnectionFactory`, `SqlNodeDirectoryDialect Dialect`, and optional `TableName`.
- `SqlNodeDirectorySchema.EnsureCreatedAsync(DbConnection, SqlNodeDirectoryDialect, string tableName = "ulinkgame_cluster_nodes")` creates one table.
- Table columns:
  - `cluster_name`
  - `node_id`
  - `node_epoch`
  - `state`
  - `endpoints_json`
  - `services_json`
  - `labels_json`
  - `lease_expires_at`
  - `updated_at`
- Use `System.Text.Json` for endpoints/services/labels.
- Use `DbCommand` and parameters; do not string-concatenate user data into SQL.
- `RegisterAsync` must run in a transaction, select current epoch, then insert/update with `epoch + 1`.
- Keep provider dependencies out of this package.

- [ ] **Step 6: Add solution entry**

Modify `Tests/tests.slnx` to include `Tests/ULinkGame.Cluster.Sql.Tests/ULinkGame.Cluster.Sql.Tests.csproj` using the existing solution file format.

- [ ] **Step 7: Run SQL tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Sql.Tests/ULinkGame.Cluster.Sql.Tests.csproj
```

Expected: tests pass.

- [ ] **Step 8: Run full test solution**

Run:

```powershell
dotnet test Tests/tests.slnx
```

Expected: all tests pass.

- [ ] **Step 9: Commit**

```powershell
git add src/ULinkGame.Cluster.Sql Tests/ULinkGame.Cluster.Sql.Tests Tests/tests.slnx
git commit -m "Add SQL-backed node directory"
```

---

### Task 4: ULinkRPC Node Directory Adapter

**Files:**
- Create: `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectoryMessages.cs`
- Create: `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectoryRecordConverter.cs`
- Create: `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectory.cs`
- Create: `src/ULinkGame.Cluster.ULinkRPC/Nodes/ULinkRpcNodeDirectoryBinder.cs`
- Modify: `src/ULinkGame.Cluster.ULinkRPC/Protocol/ULinkRpcClusterProtocol.cs`
- Create: `Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkRpcNodeDirectoryTests.cs`

- [ ] **Step 1: Write adapter tests**

Use `ULinkRpcRouteDirectoryTests.cs` as the local pattern. Add tests for:

```csharp
[Fact]
public async Task RegisterResolveHeartbeatAndExpireUseRemoteDirectory()
{
    var directory = new InMemoryNodeDirectory();
    using var harness = new RpcHarness(registry => ULinkRpcNodeDirectoryBinder.Bind(registry, directory));
    var remote = new ULinkRpcNodeDirectory(harness.Client);
    var now = DateTimeOffset.UtcNow;

    var registered = await remote.RegisterAsync(TestRegistration("local", "node-a", now), now);
    var heartbeat = await remote.HeartbeatAsync("local", "node-a", registered.Record!.NodeEpoch, now.AddSeconds(40), now.AddSeconds(1));
    var resolved = await remote.ResolveAsync("local", "node-a", now.AddSeconds(2));

    Assert.Equal(NodeRegistrationStatus.Registered, registered.Status);
    Assert.Equal(NodeHeartbeatStatus.Refreshed, heartbeat);
    Assert.NotNull(resolved);
    Assert.Equal(registered.Record.NodeEpoch, resolved!.NodeEpoch);
}
```

```csharp
[Fact]
public async Task QueryReturnsServiceFilteredNodes()
{
    var directory = new InMemoryNodeDirectory();
    using var harness = new RpcHarness(registry => ULinkRpcNodeDirectoryBinder.Bind(registry, directory));
    var remote = new ULinkRpcNodeDirectory(harness.Client);
    var now = DateTimeOffset.UtcNow;
    await remote.RegisterAsync(TestRegistration("local", "gateway-1", now, "gateway"), now);
    await remote.RegisterAsync(TestRegistration("local", "room-1", now, "room"), now);

    var records = await remote.QueryAsync(new NodeDirectoryQuery("local", serviceKind: "room"), now);

    Assert.Single(records);
    Assert.Equal("room-1", records[0].NodeId.Value);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkGame.Cluster.ULinkRPC.Tests.csproj --filter ULinkRpcNodeDirectoryTests
```

Expected: compile fails because ULinkRPC node-directory types do not exist.

- [ ] **Step 3: Add protocol IDs**

Modify `ULinkRpcClusterProtocol.cs`:

```csharp
public const int RegisterNodeMethodId = 20;
public const int HeartbeatNodeMethodId = 21;
public const int UpdateNodeStateMethodId = 22;
public const int ResolveNodeMethodId = 23;
public const int QueryNodesMethodId = 24;
public const int ExpireNodesMethodId = 25;
```

Add matching `RpcMethod<,>` fields for each request/reply DTO.

- [ ] **Step 4: Implement messages/converter/client/binder**

Follow the existing route-directory pattern:

- DTOs use primitive values and dictionaries.
- Client validates unknown enum values by mapping to conservative failure statuses:
  - registration: `InvalidRegistration`
  - heartbeat: `EpochMismatch`
  - state update: `EpochMismatch`
- Binder throws `InvalidOperationException` only for structurally invalid required payloads.
- Binder delegates all semantic decisions to `INodeDirectory`.

- [ ] **Step 5: Run adapter tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkGame.Cluster.ULinkRPC.Tests.csproj --filter ULinkRpcNodeDirectoryTests
```

Expected: tests pass.

- [ ] **Step 6: Run ULinkRPC cluster tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkGame.Cluster.ULinkRPC.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/ULinkGame.Cluster.ULinkRPC/Nodes src/ULinkGame.Cluster.ULinkRPC/Protocol/ULinkRpcClusterProtocol.cs Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkRpcNodeDirectoryTests.cs
git commit -m "Add ULinkRPC node directory adapter"
```

---

### Task 5: Diagnostics And Dependency Probes

**Files:**
- Modify: `src/ULinkGame.Cluster/Diagnostics/ClusterDiagnostics.cs`
- Modify: `src/ULinkGame.Cluster.ULinkRPC/Diagnostics/ULinkRpcClusterDependencyProbe.cs`
- Modify: `Tests/ULinkGame.Cluster.Tests/ClusterDiagnosticsTests.cs`
- Modify: `Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkRpcClusterDependencyProbeTests.cs`

- [ ] **Step 1: Add diagnostics tests**

Add low-cardinality assertions for node directory operations:

```csharp
[Fact]
public void NodeDirectoryMetricNamesAreStable()
{
    Assert.Equal("ulinkgame.cluster.node_directory.registration", ClusterDiagnostics.NodeDirectoryRegistrationMetricName);
    Assert.Equal("ulinkgame.cluster.node_directory.heartbeat", ClusterDiagnostics.NodeDirectoryHeartbeatMetricName);
    Assert.Equal("ulinkgame.cluster.node_directory.expired", ClusterDiagnostics.NodeDirectoryExpiredMetricName);
}
```

Add dependency probe test:

```csharp
[Fact]
public async Task ProbeReportsNodeDirectoryDependency()
{
    var nodeDirectory = new InMemoryNodeDirectory();
    var probe = ULinkRpcClusterDependencyProbe.ForNodeDirectory(nodeDirectory, "local", "node-a");

    var health = await probe.CheckAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ULinkRpcClusterDependencyStatus.Healthy, health.Status);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.Tests/ULinkGame.Cluster.Tests.csproj --filter ClusterDiagnosticsTests
dotnet test Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkGame.Cluster.ULinkRPC.Tests.csproj --filter ULinkRpcClusterDependencyProbeTests
```

Expected: tests fail until diagnostics/probe APIs are added.

- [ ] **Step 3: Add diagnostics constants and probe support**

Add constants only; do not add high-cardinality tags such as node id or endpoint address as metric dimensions.

Required metric names:

```csharp
public const string NodeDirectoryRegistrationMetricName = "ulinkgame.cluster.node_directory.registration";
public const string NodeDirectoryHeartbeatMetricName = "ulinkgame.cluster.node_directory.heartbeat";
public const string NodeDirectoryExpiredMetricName = "ulinkgame.cluster.node_directory.expired";
```

Probe behavior:

- Resolve a configured node or query for `node-directory` service.
- Return healthy when the directory call completes.
- Return unhealthy when the directory call throws or times out.

- [ ] **Step 4: Run diagnostics tests**

Run both commands from Step 2 again.

Expected: tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ULinkGame.Cluster/Diagnostics src/ULinkGame.Cluster.ULinkRPC/Diagnostics Tests/ULinkGame.Cluster.Tests/ClusterDiagnosticsTests.cs Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkRpcClusterDependencyProbeTests.cs
git commit -m "Add node directory diagnostics"
```

---

### Task 6: Samples, Tool Templates, And Docs Alignment

**Files:**
- Modify: `samples/Cluster.TwoNode/Program.cs`
- Modify: `samples/Cluster.TwoNode/README.md`
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Modify: `src/ULinkGame.Tool/README.md`
- Modify: `src/ULinkGame.Cluster/README.md`
- Modify: `src/ULinkGame.Cluster.ULinkRPC/README.md`
- Modify: `CONTRIBUTING.md`
- Modify: `CHANGELOG.md`
- Test: `Tests/ULinkGame.Cluster.ULinkRPC.Tests/ClusterTwoNodeSampleTests.cs`

- [ ] **Step 1: Update sample test expectations**

Extend `ClusterTwoNodeSampleTests` to assert the driver output includes:

```txt
node-directory-ready
node-registered node=worker epoch=1
node-restarted node=worker epoch=2
```

- [ ] **Step 2: Run sample test to verify it fails**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkGame.Cluster.ULinkRPC.Tests.csproj --filter ClusterTwoNodeSampleTests
```

Expected: fails because sample does not yet use node directory.

- [ ] **Step 3: Update sample**

Modify `samples/Cluster.TwoNode/Program.cs` so:

- directory mode hosts both `InMemoryNodeDirectory` and `InMemoryRouteDirectory`.
- worker registers with node directory before registering routes.
- worker uses assigned `NodeEpoch` from registration instead of command-line epoch as the production path.
- restart path demonstrates epoch increment.
- route locations use metadata:

```csharp
new Dictionary<string, string>
{
    ["service.kind"] = "room",
    ["service.name"] = "worker-room"
}
```

- [ ] **Step 4: Update tool template configuration**

Change cluster template output from flat fields:

```json
"NodeEpoch": 1,
"InternalEndpoint": "tcp://127.0.0.1:21000",
"RouteDirectoryEndpoint": "tcp://127.0.0.1:21001"
```

to planned shape:

```json
"AdvertisedEndpoints": {
  "cluster": "tcp://127.0.0.1:21000"
},
"Bootstrap": {
  "NodeDirectoryEndpoints": [
    "tcp://127.0.0.1:21000"
  ]
},
"NodeDirectory": {
  "Enabled": true,
  "Storage": {
    "Mode": "InMemory"
  }
},
"Services": [
  { "Kind": "node-directory", "Name": "node-directory" },
  { "Kind": "route-directory", "Name": "route-directory" },
  { "Kind": "gateway", "Name": "gateway" }
]
```

Update health check validation accordingly:

- Require `Cluster:NodeId`.
- Require at least one `Cluster:Services` item.
- Require at least one `Cluster:AdvertisedEndpoints` item.
- Require persistent node-directory storage when template profile is production-oriented.

- [ ] **Step 5: Update docs and changelog**

Update docs to remove "planned" wording for implemented APIs, but keep external persistent provider examples clear.

Add `CHANGELOG.md` unreleased bullet:

```markdown
- Added node-directory contracts, in-memory and SQL-backed storage, ULinkRPC node-directory adapter, and node-local service configuration scaffolding for cluster deployments.
```

- [ ] **Step 6: Run sample test**

Run:

```powershell
dotnet test Tests/ULinkGame.Cluster.ULinkRPC.Tests/ULinkGame.Cluster.ULinkRPC.Tests.csproj --filter ClusterTwoNodeSampleTests
```

Expected: tests pass.

- [ ] **Step 7: Run full test solution**

Run:

```powershell
dotnet test Tests/tests.slnx
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```powershell
git add samples/Cluster.TwoNode src/ULinkGame.Tool src/ULinkGame.Cluster/README.md src/ULinkGame.Cluster.ULinkRPC/README.md CONTRIBUTING.md CHANGELOG.md Tests/ULinkGame.Cluster.ULinkRPC.Tests/ClusterTwoNodeSampleTests.cs
git commit -m "Wire node directory into cluster samples and templates"
```

---

## Self-Review

- Spec coverage: covers node-level membership, node-local services, static bootstrap, dynamic node directory, first-version persistence, ULinkRPC access, samples/templates, and diagnostics.
- Placeholder scan: no placeholder markers remain. Each task has exact files, commands, and expected outcomes.
- Type consistency: `NodeEpoch` remains node-level. Services are descriptors inside `NodeRegistration`/`NodeRecord`; there is no service-instance registration model.
- Scope note: SQL persistence is split into `ULinkGame.Cluster.Sql` so core cluster contracts stay provider-neutral. SQLite is used only for adapter tests; production drivers remain application-owned references.
