using Agar.Godot.Sample.State.Contracts.Users;
using System.Security.Cryptography;
using System.Text;
using ULinkGame.Server.Actors;

namespace Agar.Godot.Sample.State.Users;

public sealed class UserState
{
    public string UserId { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string SessionToken { get; set; } = "";
    public int LoginCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastLoginAtUtc { get; set; }
    public bool IsOnline { get; set; }
    public float Score { get; set; }
    public int WinCount { get; set; }
}

public sealed class UserActor : Actor, IUserActor
{
    private bool _recordExists;
    private UserState _state = new();

    public Task<UserLoginResult> LoginAsync(string password)
    {
        return LoginAsync(password, reconnect: false);
    }

    public Task<UserLoginResult> LoginAsync(string password, bool reconnect)
    {
        var userId = Context.Id.Value;
        var passwordHash = ComputePasswordHash(password);
        var now = DateTime.UtcNow;

        if (!_recordExists)
        {
            _state = new UserState
            {
                UserId = userId,
                PasswordHash = passwordHash,
                CreatedAtUtc = now,
                Score = 0f
            };
            _recordExists = true;
        }
        else if (!string.Equals(_state.PasswordHash, passwordHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid password.");
        }

        if (!reconnect || string.IsNullOrWhiteSpace(_state.SessionToken))
        {
            _state.SessionToken = Guid.NewGuid().ToString("N");
        }

        _state.LoginCount += 1;
        _state.LastLoginAtUtc = now;
        _state.IsOnline = true;
        _state.Score = NormalizeScore(_state.Score);

        return Task.FromResult(new UserLoginResult
        {
            UserId = _state.UserId,
            SessionToken = _state.SessionToken,
            LoginCount = _state.LoginCount,
            LastLoginAtUtc = _state.LastLoginAtUtc,
            Score = NormalizeScore(_state.Score),
            WinCount = Math.Max(0, _state.WinCount)
        });
    }

    public Task<UserProfileSnapshot> GetProfileAsync()
    {
        var snapshot = new UserProfileSnapshot
        {
            UserId = _state.UserId,
            LoginCount = _state.LoginCount,
            CreatedAtUtc = _state.CreatedAtUtc,
            LastLoginAtUtc = _state.LastLoginAtUtc,
            IsOnline = _state.IsOnline,
            Score = NormalizeScore(_state.Score),
            WinCount = Math.Max(0, _state.WinCount)
        };
        return Task.FromResult(snapshot);
    }

    public Task SetOnlineAsync(bool isOnline)
    {
        if (_recordExists)
        {
            _state.IsOnline = isOnline;
        }

        return Task.CompletedTask;
    }

    public Task SetScoreAsync(int score)
    {
        if (_recordExists)
        {
            _state.Score = NormalizeScore(score);
        }

        return Task.CompletedTask;
    }

    public Task AddScoreAsync(int delta)
    {
        if (_recordExists)
        {
            _state.Score = Math.Max(0f, _state.Score + delta);
        }

        return Task.CompletedTask;
    }

    public Task AddWinAsync()
    {
        if (_recordExists)
        {
            _state.WinCount = Math.Max(0, _state.WinCount + 1);
        }

        return Task.CompletedTask;
    }

    private static int NormalizeScore(float score)
    {
        return Math.Max(0, (int)Math.Round(score, MidpointRounding.AwayFromZero));
    }

    private static string ComputePasswordHash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
