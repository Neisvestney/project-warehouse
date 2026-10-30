using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Auth;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Tests;

public class TokenServiceTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresFact]
    public async Task LostResponseRetryReturnsSamePairAfterGracePeriod()
    {
        var initial = await IssueAsync();
        var rotated = await RefreshAsync(initial.RefreshToken);
        await AgeAsync(initial.RefreshToken, 11);

        var recovered = await RefreshAsync(initial.RefreshToken);

        Assert.Equal(rotated.AccessToken, recovered.AccessToken);
        Assert.Equal(rotated.RefreshToken, recovered.RefreshToken);
        Assert.InRange(recovered.ExpiresIn, 1, rotated.ExpiresIn);
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.RefreshTokens.SingleAsync(t => t.TokenHash == Hash(initial.RefreshToken));
        Assert.DoesNotContain(rotated.RefreshToken, original.RotationResponseProtected!);
        Assert.DoesNotContain(rotated.AccessToken, original.RotationResponseProtected!);
        Assert.Equal(2, await db.RefreshTokens.CountAsync(t => t.UserId == original.UserId));
    }

    [PostgresFact]
    public async Task ConcurrentRefreshesReturnSamePair()
    {
        var initial = await IssueAsync();
        var results = await Task.WhenAll(RefreshAsync(initial.RefreshToken), RefreshAsync(initial.RefreshToken));
        Assert.Equal(results[0].AccessToken, results[1].AccessToken);
        Assert.Equal(results[0].RefreshToken, results[1].RefreshToken);
    }

    [PostgresFact]
    public async Task LoggedOutReplacementCannotBeRecovered()
    {
        var initial = await IssueAsync();
        var rotated = await RefreshAsync(initial.RefreshToken);
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == Hash(rotated.RefreshToken))
            .Select(t => t.UserId).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<ITokenService>()
            .RevokeRefreshTokenAsync(rotated.RefreshToken, userId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(initial.RefreshToken));
        Assert.Equal(0, await db.Users.Where(u => u.Id == userId).Select(u => u.SecurityVersion).SingleAsync());
    }

    [PostgresTheory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task LogoutOriginalRevokesRotationChainAndPreservesOtherSessions(int rotations)
    {
        var initial = await IssueAsync();
        var chain = new List<TokenResponse> {initial};
        for (var i = 0; i < rotations; i++)
            chain.Add(await RefreshAsync(chain[^1].RefreshToken));

        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == Hash(initial.RefreshToken))
            .Select(t => t.UserId).SingleAsync();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        var service = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var otherSession = await service.IssueTokensAsync(user);

        await service.RevokeRefreshTokenAsync(initial.RefreshToken, userId);
        await service.RevokeRefreshTokenAsync(initial.RefreshToken, userId);

        foreach (var pair in chain)
            await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(pair.RefreshToken));
        Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null));
        Assert.Equal(0, await db.Users.Where(u => u.Id == userId).Select(u => u.SecurityVersion).SingleAsync());
        Assert.NotNull(await RefreshAsync(otherSession.RefreshToken));
    }

    [PostgresFact]
    public async Task LogoutRacingWithRefreshLeavesNoActiveReplacement()
    {
        var initial = await IssueAsync();
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == Hash(initial.RefreshToken))
            .Select(t => t.UserId).SingleAsync();
        var service = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var refresh = Record.ExceptionAsync(() => RefreshAsync(initial.RefreshToken));
        await Task.WhenAll(refresh, service.RevokeRefreshTokenAsync(initial.RefreshToken, userId));

        Assert.True(await refresh is null or InvalidOperationException);
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == userId && t.RevokedAt == null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(initial.RefreshToken));
    }

    [PostgresFact]
    public async Task SecurityVersionChangePreventsRecovery()
    {
        var initial = await IssueAsync();
        await RefreshAsync(initial.RefreshToken);
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == Hash(initial.RefreshToken))
            .Select(t => t.UserId).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<SecurityVersionStore>().BumpAsync(userId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(initial.RefreshToken));
    }

    [PostgresFact]
    public async Task ConsumedReplacementCannotBeRecovered()
    {
        var initial = await IssueAsync();
        var rotated = await RefreshAsync(initial.RefreshToken);
        var latest = await RefreshAsync(rotated.RefreshToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(initial.RefreshToken));
        Assert.NotNull(await RefreshAsync(latest.RefreshToken));
    }

    [PostgresFact]
    public async Task ReuseOutsideRecoveryRevokesAllSessionsAndEvictsVersion()
    {
        var initial = await IssueAsync();
        var rotated = await RefreshAsync(initial.RefreshToken);
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == Hash(initial.RefreshToken))
            .Select(t => t.UserId).SingleAsync();
        var versions = scope.ServiceProvider.GetRequiredService<SecurityVersionStore>();
        Assert.Equal(0, await versions.GetVersionAsync(userId));
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        await scope.ServiceProvider.GetRequiredService<ITokenService>().IssueTokensAsync(user);
        await AgeAsync(initial.RefreshToken, 301);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(initial.RefreshToken));

        Assert.Equal(1, await versions.GetVersionAsync(userId));
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == userId && t.RevokedAt == null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(rotated.RefreshToken));
    }

    [PostgresFact]
    public async Task FailedRotationRollsBackReplacementAndKeepsOriginalActive()
    {
        var initial = await IssueAsync();
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var service = ActivatorUtilities.CreateInstance<TokenService>(scope.ServiceProvider,
                new FailingProtectionProvider());
            await Assert.ThrowsAsync<CryptographicException>(() => service.RefreshAsync(initial.RefreshToken));
        }

        await using var check = database.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.RefreshTokens.SingleAsync(t => t.TokenHash == Hash(initial.RefreshToken));
        Assert.Null(original.RevokedAt);
        Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == original.UserId));
        Assert.NotNull(await RefreshAsync(initial.RefreshToken));
    }

    private async Task<TokenResponse> IssueAsync()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser {Id = Guid.NewGuid(), UserName = "rotation-test"};
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await scope.ServiceProvider.GetRequiredService<ITokenService>().IssueTokensAsync(user);
    }

    private async Task<TokenResponse> RefreshAsync(string token)
    {
        await using var scope = database.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITokenService>().RefreshAsync(token);
    }

    private async Task AgeAsync(string token, int seconds)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.RefreshTokens.Where(t => t.TokenHash == Hash(token))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow.AddSeconds(-seconds)));
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private sealed class FailingProtectionProvider : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => throw new CryptographicException("Simulated encryption failure.");
        public byte[] Unprotect(byte[] protectedData) => throw new NotSupportedException();
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PW_TEST_POSTGRES")))
            Skip = "Set PW_TEST_POSTGRES to run isolated PostgreSQL integration tests.";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PW_TEST_POSTGRES")))
            Skip = "Set PW_TEST_POSTGRES to run isolated PostgreSQL integration tests.";
    }
}

public sealed class TokenDatabase : IAsyncLifetime
{
    private readonly string _databaseName = "pw_token_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("PW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(_adminConnection) {Database = _databaseName};
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new NpgsqlDataSourceBuilder(connection.ConnectionString).EnableDynamicJson().Build());
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()).UseProjectables());
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.Configure<JwtOptions>(options => options.SecretKey = new string('s', 64));
        services.AddSingleton<SecurityVersionStore>();
        services.AddScoped<IEntityLockService, EntityLockService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IPermissionService, EmptyPermissions>();
        Services = services.BuildServiceProvider();
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_adminConnection is null) return;
        if (Services is not null) await Services.DisposeAsync();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed class EmptyPermissions : IPermissionService
    {
        public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task BumpForRoleUsersAsync(Guid roleId) => Task.CompletedTask;
        public Task BumpUsersAsync(IEnumerable<Guid> userIds) => Task.CompletedTask;
    }
}
