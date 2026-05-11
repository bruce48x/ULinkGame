using Orleans.Storage;

namespace ULinkRPC.Sample.Silo.Persistence;

public sealed class DapperGrainStorageOptions : IStorageProviderSerializerOptions
{
    public string ConnectionString { get; set; } = "";

    public string TableName { get; set; } = AgarSiloStorageNames.GrainStateTable;

    public IGrainStorageSerializer GrainStorageSerializer { get; set; } = default!;
}
