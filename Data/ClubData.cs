using Dapper;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Genealogy.Data;

public sealed class ClubStorage(IConfiguration configuration, IWebHostEnvironment environment)
{
    public const long MaxUploadBytes = 100 * 1024 * 1024;
    public const long MaxRequestBytes = MaxUploadBytes + 1024 * 1024;

    public string Root { get; } = Path.GetFullPath(
        configuration["Club:DataDirectory"] ?? "App_Data", environment.ContentRootPath);

    public string KeyDirectory => Path.Combine(Root, "keys");
    public string FileDirectory => Path.Combine(Root, "uploads");

    public string PathFor(string key) => Path.Combine(FileDirectory, Guid.ParseExact(key, "N").ToString("N"));

    public void Prepare()
    {
        Directory.CreateDirectory(KeyDirectory);
        Directory.CreateDirectory(FileDirectory);
    }
}

public sealed class ClubClock(IConfiguration configuration)
{
    public const string LocalFormat = "yyyy-MM-dd'T'HH:mm";
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Club:TimeZone"] ?? "America/New_York");

    public string Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone)
        .ToString(LocalFormat, CultureInfo.InvariantCulture);

    public string ZoneLabel(DateTime time)
    {
        var offset = Zone.GetUtcOffset(time);
        return Zone.Id is "America/New_York" or "Eastern Standard Time"
            ? "ET"
            : $"UTC{(offset.TotalMinutes < 0 ? "-" : "+")}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";
    }
}

public sealed record Meeting(long Id, string Title, string StartsAt, string? Location, string? Description)
{
    public DateTime LocalTime => DateTime.ParseExact(StartsAt, ClubClock.LocalFormat, CultureInfo.InvariantCulture);
}

public sealed record HomepageContent(string Mission, string HeroHeadline, string HeroIntroduction, string? ClubEmail);

public sealed record Resource(
    long Id, string Title, string Kind, string? Description, bool MembersOnly,
    string? ExternalUrl, string? StorageKey, string? OriginalName, string? ContentType, long Revision)
{
    public Resource() : this(0, "", "", null, false, null, null, null, null, 0) { }
}

public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string Member = "Member";
}

public sealed record ClubAccount(
    long Id, string Email, string PasswordHash, string Role, bool IsActive, long SessionVersion)
{
    public ClubAccount() : this(0, "", "", "", false, 0) { }
}

public sealed class ClubData(ClubStorage storage)
{
    public const string DraftMission =
        "We bring Colonial Heritage neighbors together to explore family history, share research skills, and preserve the stories of generations past. Whether you’re opening your first family record or tracing a line you’ve followed for years, there’s a place for you here.";

    private const string DefaultHeroHeadline = "Every family\nhas a *story.*";
    private const string DefaultHeroIntroduction =
        "Discover yours with the Colonial Heritage Genealogy Club. Explore your roots and share what you find with neighbors who love family history.";

    private const string MeetingColumns = "Id, Title, StartsAt, Location, Description";
    private const string ResourceColumns =
        "Id, Title, Kind, Description, MembersOnly, ExternalUrl, StorageKey, OriginalName, ContentType, Revision";
    private const string AccountColumns = "Id, Email, PasswordHash, Role, IsActive, SessionVersion";

    private SqliteConnection Connect() => new(new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(storage.Root, "club.db"),
        ForeignKeys = true,
        DefaultTimeout = 10
    }.ToString());

    public async Task InitializeAsync()
    {
        await using var connection = Connect();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction(deferred: false);
        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version;", transaction: transaction);
        if (version > 3)
            throw new InvalidOperationException("This database was created by a newer version of the site.");

        if (version == 0)
        {
            await connection.ExecuteAsync("""
                CREATE TABLE ClubSettings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Mission TEXT NOT NULL
                );
                CREATE TABLE Meetings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    StartsAt TEXT NOT NULL,
                    Location TEXT,
                    Description TEXT
                );
                CREATE INDEX IX_Meetings_StartsAt ON Meetings (StartsAt, Id);
                CREATE TABLE Resources (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Kind TEXT NOT NULL CHECK (Kind IN ('Slides', 'Video')),
                    Description TEXT,
                    MembersOnly INTEGER NOT NULL CHECK (MembersOnly IN (0, 1)),
                    ExternalUrl TEXT,
                    StorageKey TEXT UNIQUE,
                    OriginalName TEXT,
                    ContentType TEXT,
                    Revision INTEGER NOT NULL DEFAULT 1,
                    CHECK ((ExternalUrl IS NULL) <> (StorageKey IS NULL)),
                    CHECK (ExternalUrl IS NULL OR MembersOnly = 0)
                );
                CREATE TABLE Accounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Role TEXT NOT NULL CHECK (Role IN ('Admin', 'Member')),
                    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
                    SessionVersion INTEGER NOT NULL DEFAULT 1
                );
                INSERT INTO ClubSettings (Id, Mission) VALUES (1, @Mission);
                PRAGMA user_version = 1;
                """, new { Mission = DraftMission }, transaction);
        }

        if (version < 2)
        {
            await connection.ExecuteAsync("""
                ALTER TABLE ClubSettings ADD COLUMN HeroHeadline TEXT NOT NULL DEFAULT '';
                ALTER TABLE ClubSettings ADD COLUMN HeroIntroduction TEXT NOT NULL DEFAULT '';
                UPDATE ClubSettings SET HeroHeadline = @Headline, HeroIntroduction = @Introduction WHERE Id = 1;
                PRAGMA user_version = 2;
                """, new { Headline = DefaultHeroHeadline, Introduction = DefaultHeroIntroduction }, transaction);
        }

        if (version < 3)
        {
            await connection.ExecuteAsync("""
                ALTER TABLE ClubSettings ADD COLUMN ClubEmail TEXT;
                PRAGMA user_version = 3;
                """, transaction: transaction);
        }

        transaction.Commit();
    }

    public async Task<HomepageContent> GetHomepageAsync()
    {
        await using var connection = Connect();
        return await connection.QuerySingleAsync<HomepageContent>(
            "SELECT Mission, HeroHeadline, HeroIntroduction, ClubEmail FROM ClubSettings WHERE Id = 1");
    }

    public async Task<string?> GetClubEmailAsync()
    {
        await using var connection = Connect();
        return await connection.QuerySingleAsync<string?>("SELECT ClubEmail FROM ClubSettings WHERE Id = 1");
    }

    public async Task SaveHomepageAsync(string headline, string introduction, string? clubEmail)
    {
        await using var connection = Connect();
        await connection.ExecuteAsync("""
            UPDATE ClubSettings SET HeroHeadline = @Headline, HeroIntroduction = @Introduction, ClubEmail = @ClubEmail
            WHERE Id = 1
            """, new { Headline = headline, Introduction = introduction, ClubEmail = clubEmail });
    }

    public async Task SaveMissionAsync(string mission)
    {
        await using var connection = Connect();
        await connection.ExecuteAsync("UPDATE ClubSettings SET Mission = @Mission WHERE Id = 1", new { Mission = mission });
    }

    public async Task<IReadOnlyList<Meeting>> ListMeetingsAsync(string? since = null, int limit = -1)
    {
        await using var connection = Connect();
        return (await connection.QueryAsync<Meeting>($"""
            SELECT {MeetingColumns} FROM Meetings
            WHERE @Since IS NULL OR StartsAt >= @Since ORDER BY StartsAt, Id LIMIT @Limit
            """, new { Since = since, Limit = limit })).AsList();
    }

    public async Task<Meeting?> FindMeetingAsync(long id)
    {
        await using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<Meeting>(
            $"SELECT {MeetingColumns} FROM Meetings WHERE Id = @Id", new { Id = id });
    }

    public async Task<bool> SaveMeetingAsync(Meeting meeting)
    {
        await using var connection = Connect();
        var sql = meeting.Id == 0
            ? "INSERT INTO Meetings (Title, StartsAt, Location, Description) VALUES (@Title, @StartsAt, @Location, @Description)"
            : "UPDATE Meetings SET Title = @Title, StartsAt = @StartsAt, Location = @Location, Description = @Description WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, meeting) == 1;
    }

    public async Task DeleteMeetingAsync(long id)
    {
        await using var connection = Connect();
        await connection.ExecuteAsync("DELETE FROM Meetings WHERE Id = @Id", new { Id = id });
    }

    public async Task<IReadOnlyList<Resource>> ListResourcesAsync(bool includePrivate, string? kind = null)
    {
        await using var connection = Connect();
        return (await connection.QueryAsync<Resource>($"""
            SELECT {ResourceColumns} FROM Resources
            WHERE (@IncludePrivate = 1 OR MembersOnly = 0) AND (@Kind IS NULL OR Kind = @Kind)
            ORDER BY Id DESC
            """, new { IncludePrivate = includePrivate, Kind = kind })).AsList();
    }

    public async Task<Resource?> FindResourceAsync(long id, bool includePrivate)
    {
        await using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<Resource>($"""
            SELECT {ResourceColumns} FROM Resources
            WHERE Id = @Id AND (@IncludePrivate = 1 OR MembersOnly = 0)
            """, new { Id = id, IncludePrivate = includePrivate });
    }

    public async Task<bool> SaveResourceAsync(Resource resource)
    {
        await using var connection = Connect();
        var sql = resource.Id == 0
            ? """
                INSERT INTO Resources (Title, Kind, Description, MembersOnly, ExternalUrl, StorageKey, OriginalName, ContentType)
                VALUES (@Title, @Kind, @Description, @MembersOnly, @ExternalUrl, @StorageKey, @OriginalName, @ContentType)
                """
            : """
                UPDATE Resources SET Title = @Title, Kind = @Kind, Description = @Description, MembersOnly = @MembersOnly,
                    ExternalUrl = @ExternalUrl, StorageKey = @StorageKey, OriginalName = @OriginalName,
                    ContentType = @ContentType, Revision = Revision + 1
                WHERE Id = @Id AND Revision = @Revision
                """;
        return await connection.ExecuteAsync(sql, resource) == 1;
    }

    public async Task<string?> DeleteResourceAsync(long id)
    {
        await using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "DELETE FROM Resources WHERE Id = @Id RETURNING StorageKey", new { Id = id });
    }

    public async Task<ClubAccount?> FindAccountByEmailAsync(string email)
    {
        await using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<ClubAccount>(
            $"SELECT {AccountColumns} FROM Accounts WHERE Email = @Email", new { Email = email });
    }

    public async Task<ClubAccount?> FindAccountAsync(long id)
    {
        await using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<ClubAccount>(
            $"SELECT {AccountColumns} FROM Accounts WHERE Id = @Id", new { Id = id });
    }

    public async Task<bool> BootstrapAdminAsync(string email, string hash)
    {
        await using var connection = Connect();
        return await connection.ExecuteAsync("""
            INSERT INTO Accounts (Email, PasswordHash, Role)
            SELECT @Email, @Hash, 'Admin' WHERE NOT EXISTS (SELECT 1 FROM Accounts WHERE Role = 'Admin')
            """, new { Email = email, Hash = hash }) == 1;
    }

    public async Task<IReadOnlyList<ClubAccount>> ListMembersAsync()
    {
        await using var connection = Connect();
        return (await connection.QueryAsync<ClubAccount>(
            $"SELECT {AccountColumns} FROM Accounts WHERE Role = 'Member' ORDER BY Email")).AsList();
    }

    public async Task<bool> CreateMemberAsync(string email, string hash)
    {
        await using var connection = Connect();
        return await connection.ExecuteAsync("""
            INSERT INTO Accounts (Email, PasswordHash, Role) VALUES (@Email, @Hash, 'Member')
            ON CONFLICT (Email) DO NOTHING
            """, new { Email = email, Hash = hash }) == 1;
    }

    public async Task SetMemberStatusAsync(long id, bool active)
    {
        await using var connection = Connect();
        await connection.ExecuteAsync("""
            UPDATE Accounts SET IsActive = @Active, SessionVersion = SessionVersion + 1
            WHERE Id = @Id AND Role = 'Member' AND IsActive <> @Active
            """, new { Id = id, Active = active });
    }

    public async Task SetMemberPasswordAsync(long id, string hash)
    {
        await using var connection = Connect();
        await connection.ExecuteAsync("""
            UPDATE Accounts SET PasswordHash = @Hash, SessionVersion = SessionVersion + 1
            WHERE Id = @Id AND Role = 'Member'
            """, new { Id = id, Hash = hash });
    }

    public async Task<bool> ChangePasswordAsync(ClubAccount account, string hash)
    {
        await using var connection = Connect();
        return await connection.ExecuteAsync("""
            UPDATE Accounts SET PasswordHash = @Hash, SessionVersion = SessionVersion + 1
            WHERE Id = @Id AND SessionVersion = @SessionVersion AND IsActive = 1
            """, new { account.Id, account.SessionVersion, Hash = hash }) == 1;
    }
}
