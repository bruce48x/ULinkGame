using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;

namespace ULinkRPC.Sample.Silo.Persistence;

public sealed class DapperGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly string _storageName;
    private readonly DapperGrainStorageOptions _options;
    private readonly ClusterOptions _clusterOptions;
    private readonly string _tableName;

    public DapperGrainStorage(
        string storageName,
        DapperGrainStorageOptions options,
        IOptions<ClusterOptions> clusterOptions)
    {
        _storageName = storageName;
        _options = options;
        _clusterOptions = clusterOptions.Value;
        _tableName = ValidateIdentifier(options.TableName);
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(
            observerName: OptionFormattingUtilities.Name<DapperGrainStorage>(_storageName),
            stage: ServiceLifecycleStage.ApplicationServices,
            onStart: _ => EnsureTableAsync());
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        await using var connection = CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<StoredGrainState>(
            $"""
            SELECT payload, version
            FROM {_tableName}
            WHERE service_id = @ServiceId
              AND provider_name = @ProviderName
              AND state_name = @StateName
              AND grain_id = @GrainId
            """,
            CreateKeyParameters(stateName, grainId));

        if (row is null)
        {
            grainState.State = Activator.CreateInstance<T>()!;
            grainState.ETag = null;
            grainState.RecordExists = false;
            return;
        }

        grainState.State = _options.GrainStorageSerializer.Deserialize<T>(new BinaryData(row.Payload));
        grainState.ETag = row.Version.ToString();
        grainState.RecordExists = true;
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var payload = _options.GrainStorageSerializer.Serialize(grainState.State).ToArray();
        var expectedVersion = ParseETag(grainState.ETag);

        await using var connection = CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<StoredGrainVersion>(
            $"""
            INSERT INTO {_tableName}
                (service_id, provider_name, state_name, grain_id, payload, version, modified_on_utc)
            VALUES
                (@ServiceId, @ProviderName, @StateName, @GrainId, @Payload, 1, now() at time zone 'utc')
            ON CONFLICT (service_id, provider_name, state_name, grain_id)
            DO UPDATE SET
                payload = EXCLUDED.payload,
                version = {_tableName}.version + 1,
                modified_on_utc = EXCLUDED.modified_on_utc
            WHERE @ExpectedVersion IS NOT NULL
              AND {_tableName}.version = @ExpectedVersion
            RETURNING version
            """,
            CreateWriteParameters(stateName, grainId, payload, expectedVersion));

        if (row is null)
        {
            throw CreateInconsistentStateException<T>("WriteState", stateName, grainId);
        }

        grainState.ETag = row.Version.ToString();
        grainState.RecordExists = true;
    }

    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var expectedVersion = ParseETag(grainState.ETag);
        if (expectedVersion is null)
        {
            grainState.State = Activator.CreateInstance<T>()!;
            grainState.ETag = null;
            grainState.RecordExists = false;
            return;
        }

        await using var connection = CreateConnection();
        var affectedRows = await connection.ExecuteAsync(
            $"""
            DELETE FROM {_tableName}
            WHERE service_id = @ServiceId
              AND provider_name = @ProviderName
              AND state_name = @StateName
              AND grain_id = @GrainId
              AND version = @ExpectedVersion
            """,
            CreateWriteParameters(stateName, grainId, payload: [], expectedVersion));

        if (affectedRows == 0)
        {
            throw CreateInconsistentStateException<T>("ClearState", stateName, grainId);
        }

        grainState.State = Activator.CreateInstance<T>()!;
        grainState.ETag = null;
        grainState.RecordExists = false;
    }

    private async Task EnsureTableAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Missing Dapper grain storage connection string.");
        }

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(
            $"""
            CREATE TABLE IF NOT EXISTS {_tableName} (
                service_id varchar(150) NOT NULL,
                provider_name varchar(150) NOT NULL,
                state_name varchar(150) NOT NULL,
                grain_id varchar(512) NOT NULL,
                payload bytea NOT NULL,
                version bigint NOT NULL,
                modified_on_utc timestamp without time zone NOT NULL,
                CONSTRAINT pk_{_tableName} PRIMARY KEY (service_id, provider_name, state_name, grain_id)
            )
            """);
    }

    private NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_options.ConnectionString);
    }

    private object CreateKeyParameters(string stateName, GrainId grainId)
    {
        return new
        {
            _clusterOptions.ServiceId,
            ProviderName = _storageName,
            StateName = stateName,
            GrainId = grainId.ToString()
        };
    }

    private object CreateWriteParameters(string stateName, GrainId grainId, byte[] payload, long? expectedVersion)
    {
        return new
        {
            _clusterOptions.ServiceId,
            ProviderName = _storageName,
            StateName = stateName,
            GrainId = grainId.ToString(),
            Payload = payload,
            ExpectedVersion = expectedVersion
        };
    }

    private static long? ParseETag(string? etag)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            return null;
        }

        return long.TryParse(etag, out var version)
            ? version
            : throw new InconsistentStateException($"Invalid grain state ETag: '{etag}'.");
    }

    private InconsistentStateException CreateInconsistentStateException<T>(string operation, string stateName, GrainId grainId)
    {
        return new InconsistentStateException(
            $"Version conflict ({operation}): ServiceId={_clusterOptions.ServiceId} ProviderName={_storageName} StateName={stateName} GrainType={typeof(T)} GrainId={grainId}.");
    }

    private static string ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)
            || identifier.Any(static c => !char.IsAsciiLetterOrDigit(c) && c != '_')
            || char.IsAsciiDigit(identifier[0]))
        {
            throw new InvalidOperationException($"Invalid Dapper grain storage table name: '{identifier}'.");
        }

        return identifier;
    }

    private sealed class StoredGrainState
    {
        public byte[] Payload { get; set; } = [];

        public long Version { get; set; }
    }

    private sealed class StoredGrainVersion
    {
        public long Version { get; set; }
    }
}
