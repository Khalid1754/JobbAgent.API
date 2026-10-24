using JobbAgent.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JobbAgent.Api.Data;

public class JobbAgentDbContext(DbContextOptions<JobbAgentDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<AgentStep> Steps => Set<AgentStep>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<Watch> Watches => Set<Watch>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Profile>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Name).HasMaxLength(60);
            e.Property(p => p.FileName).HasMaxLength(260);
            e.Property(p => p.FullName).HasMaxLength(100);
            e.Property(p => p.Email).HasMaxLength(200);
            e.Property(p => p.Phone).HasMaxLength(50);
            e.Property(p => p.City).HasMaxLength(100);
            e.Property(p => p.Link).HasMaxLength(300);
        });
        b.Entity<Run>(e =>
        {
            e.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Title).HasMaxLength(300);
            e.Property(r => r.Company).HasMaxLength(200);
            e.Property(r => r.Location).HasMaxLength(100);
            e.Property(r => r.Keywords).HasMaxLength(200);
            e.Property(r => r.AdUrl).HasMaxLength(1000);
            e.Property(r => r.PlatsbankenId).HasMaxLength(50);
            e.Property(r => r.Language).HasMaxLength(10);
            e.Property(r => r.UiLanguage).HasMaxLength(10).HasDefaultValue("sv");
            e.Property(r => r.Error).HasMaxLength(1000);
            e.HasIndex(r => r.CreatedAt);
            e.HasMany(r => r.Steps).WithOne().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<JobApplication>(e =>
        {
            e.Property(a => a.Title).HasMaxLength(300);
            e.Property(a => a.Company).HasMaxLength(200);
            e.Property(a => a.Location).HasMaxLength(100);
            e.Property(a => a.Url).HasMaxLength(1000);
            e.Property(a => a.PlatsbankenId).HasMaxLength(50);
            e.Property(a => a.Stage).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Notes).HasMaxLength(4000);
            e.HasIndex(a => a.PlatsbankenId);
            e.HasIndex(a => a.LetterRunId);
        });
        b.Entity<Watch>(e =>
        {
            e.Property(w => w.Location).HasMaxLength(100);
            e.Property(w => w.Keywords).HasMaxLength(200);
            e.Property(w => w.UiLanguage).HasMaxLength(10).HasDefaultValue("sv");
        });
        b.Entity<AgentStep>(e =>
        {
            e.Property(s => s.Agent).HasMaxLength(40);
            e.Property(s => s.ToAgent).HasMaxLength(40);
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Summary).HasMaxLength(500);
            e.HasIndex(s => new { s.RunId, s.Sequence }).IsUnique();
        });
    }
}
