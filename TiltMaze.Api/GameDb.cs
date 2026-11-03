using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TiltMaze.Core;

namespace TiltMaze.Api;

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

// Brugerne er Identity's egne (IdentityUser). UserName er brugernavnet, som også står på top-listen. Ingen e-mail.
// Identity har et unikt indeks på NormalizedUserName (store bogstaver), så to brugere kan ikke hedde "Marcus" og "marcus"
public class GameDb : IdentityDbContext<IdentityUser>
{
    public GameDb(DbContextOptions<GameDb> options) : base(options)
    {
    }

    public DbSet<RunEntity> Runs => Set<RunEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Slettes brugeren, slettes tiderne (og spøgelserne) også
        builder.Entity<RunEntity>()
            .HasOne<IdentityUser>()
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

// En linje på top-listen. RunId bruges til at hente spøgelset
public record LeaderboardRow(int Rank, int RunId, string Username, int TimeMs, bool IsMe);

// Et spøgelse fra en anden spiller
public record GhostResponse(string Username, int TimeMs, string Ghost);

public record CreateAccountRequest(string? Username, string? Password);

public record LoginRequest(string? Username, string? Password);

public record RefreshRequest(string? RefreshToken);

public record UsernameRequest(string? Username);

public record AccountResponse(string Username);

public record AvailableResponse(bool Available);
