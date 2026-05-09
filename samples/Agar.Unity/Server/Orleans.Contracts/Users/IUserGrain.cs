using Orleans;

namespace Orleans.Contracts.Users;

public interface IUserGrain : IGrainWithStringKey
{
    Task<UserLoginResult> LoginAsync(string password);
    Task<UserLoginResult> LoginAsync(string password, bool reconnect);
    Task<UserProfileSnapshot> GetProfileAsync();
    Task SetOnlineAsync(bool isOnline);
    Task AddWinAsync();
    Task AddVictoryPointsAsync(int points);
    Task ResetVictoryPointsAsync();
}

[GenerateSerializer]
public sealed class UserLoginResult
{
    [Id(0)]
    public string UserId { get; set; } = "";

    [Id(1)]
    public string SessionToken { get; set; } = "";

    [Id(2)]
    public int LoginCount { get; set; }

    [Id(3)]
    public DateTime LastLoginAtUtc { get; set; }

    [Id(4)]
    public int WinCount { get; set; }
    [Id(5)]
    public int VictoryPoints { get; set; }
}

[GenerateSerializer]
public sealed class UserProfileSnapshot
{
    [Id(0)]
    public string UserId { get; set; } = "";

    [Id(1)]
    public int LoginCount { get; set; }

    [Id(2)]
    public DateTime CreatedAtUtc { get; set; }

    [Id(3)]
    public DateTime LastLoginAtUtc { get; set; }

    [Id(4)]
    public bool IsOnline { get; set; }

    [Id(5)]
    public int WinCount { get; set; }

    [Id(6)]
    public int VictoryPoints { get; set; }
}

