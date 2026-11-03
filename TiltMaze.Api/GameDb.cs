using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TiltMaze.Core;

namespace TiltMaze.Api;

// Brugeren er Identity's bruger plus et kaldenavn, som vises på top-listen (aldrig e-mailen)
public class AppUser : IdentityUser
{
    public string? Nickname { get; set; }
}

// En brugers bedste tid på én labyrint (sværhedsgrad + seed), med spøgelset der beviser den
public class RunEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public Difficulty Difficulty { get; set; }
    public int Seed { get; set; }
    public int TimeMs { get; set; }
    public byte[] Ghost { get; set; } = [];   // float32 x,y hvert 1/30 s
    public DateTime CreatedAt { get; set; }
}

public class GameDb : IdentityDbContext<AppUser>
{
    public GameDb(DbContextOptions<GameDb> options) : base(options)
    {
    }

    public DbSet<RunEntity> Runs => Set<RunEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Kaldenavne er unikke (store/små bogstaver er ens). Brugere uden kaldenavn (null) må der gerne være flere af
        builder.Entity<AppUser>().Property(u => u.Nickname).UseCollation("NOCASE");
        builder.Entity<AppUser>().HasIndex(u => u.Nickname).IsUnique();

        // Slettes brugeren, slettes tiderne også
        builder.Entity<RunEntity>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kun én (den bedste) tid pr. bruger og labyrint
        builder.Entity<RunEntity>().HasIndex(r => new { r.UserId, r.Difficulty, r.Seed }).IsUnique();

        // Top-listen henter hurtigste tider på en labyrint
        builder.Entity<RunEntity>().HasIndex(r => new { r.Difficulty, r.Seed, r.TimeMs });
    }
}

// Tid fra appen. Ghost er base64 af float32 (lille endian), se Ghost i Core
public record RunRequest(Difficulty Difficulty, int Seed, int TimeMs, string Ghost);

// Svar efter en tid: er den brugerens nye bedste, og hvilken plads giver den
public record RunResponse(int Id, int TimeMs, bool IsBest, int Rank);

// En linje på top-listen. RunId bruges til at hente spøgelset. Aldrig e-mail
public record LeaderboardRow(int Rank, int RunId, string Nickname, int TimeMs, bool IsMe);

// Et spøgelse fra en anden spiller
public record GhostResponse(string Nickname, int TimeMs, string Ghost);

public record CreateAccountRequest(string? Email, string? Password, string? Nickname);

public record NicknameRequest(string? Nickname);
