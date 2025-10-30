using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AgentEntity> Agents { get; set; } = null!;
    public DbSet<AgentVersionEntity> AgentVersions { get; set; } = null!;
    public DbSet<DeploymentEntity> Deployments { get; set; } = null!;
    public DbSet<NodeEntity> Nodes { get; set; } = null!;
    public DbSet<RunEntity> Runs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Agent entity
        modelBuilder.Entity<AgentEntity>(entity =>
        {
            entity.HasKey(e => e.AgentId);
            entity.HasIndex(e => e.Name);
        });

        // Configure AgentVersion entity
        modelBuilder.Entity<AgentVersionEntity>(entity =>
        {
            entity.HasKey(e => e.VersionId);
            entity.HasIndex(e => new { e.AgentId, e.Version }).IsUnique();
            
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Versions)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Deployment entity
        modelBuilder.Entity<DeploymentEntity>(entity =>
        {
            entity.HasKey(e => e.DepId);
            entity.HasIndex(e => new { e.AgentId, e.Version, e.Env });
            
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Deployments)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Node entity
        modelBuilder.Entity<NodeEntity>(entity =>
        {
            entity.HasKey(e => e.NodeId);
            entity.HasIndex(e => e.HeartbeatAt);
        });

        // Configure Run entity
        modelBuilder.Entity<RunEntity>(entity =>
        {
            entity.HasKey(e => e.RunId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.AgentId, e.Status });
            entity.HasIndex(e => e.NodeId);
            
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Runs)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(e => e.Deployment)
                .WithMany(d => d.Runs)
                .HasForeignKey(e => e.DepId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.Node)
                .WithMany(n => n.Runs)
                .HasForeignKey(e => e.NodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
