using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlPlane.Api.Data;

[Table("deployments")]
public class DeploymentEntity
{
    [Key]
    [Column("dep_id")]
    public string DepId { get; set; } = string.Empty;

    [Required]
    [Column("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [Column("version")]
    public string Version { get; set; } = string.Empty;

    [Required]
    [Column("env")]
    public string Env { get; set; } = string.Empty;

    [Column("target", TypeName = "jsonb")]
    public string? Target { get; set; }

    [Column("status", TypeName = "jsonb")]
    public string? Status { get; set; }

    [ForeignKey(nameof(AgentId))]
    public AgentEntity Agent { get; set; } = null!;

    public ICollection<RunEntity> Runs { get; set; } = new List<RunEntity>();
}
