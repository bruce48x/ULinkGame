
namespace Agar.Godot.Sample.State.Contracts.Users;

public interface IUserActor
{
    Task<UserLoginResult> LoginAsync(string password);
    Task<UserLoginResult> LoginAsync(string password, bool reconnect);
    Task<UserProfileSnapshot> GetProfileAsync();
    Task SetOnlineAsync(bool isOnline);
    Task SetScoreAsync(int score);
    Task AddScoreAsync(int delta);
    Task AddWinAsync();
}

public sealed class UserLoginResult
{
    public string UserId { get; set; } = "";
    public string SessionToken { get; set; } = "";
    public int LoginCount { get; set; }
    public DateTime LastLoginAtUtc { get; set; }
    public int Score { get; set; }
    public int WinCount { get; set; }
}

public sealed class UserProfileSnapshot
{
    public string UserId { get; set; } = "";
    public int LoginCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastLoginAtUtc { get; set; }
    public bool IsOnline { get; set; }
    public int Score { get; set; }
    public int WinCount { get; set; }
}

