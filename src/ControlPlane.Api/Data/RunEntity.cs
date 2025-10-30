using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlPlane.Api.Data;

[Table("runs")]
public class RunEntity
{
    [Key]
    [Column("run_id")]
    public string RunId { get; set; } = string.Empty;

    [Required]
    [Column("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [Column("version")]
    public string Version { get; set; } = string.Empty;

    [Column("dep_id")]
    public string? DepId { get; set; }

    [Column("node_id")]
    public string? NodeId { get; set; }

    [Column("input_ref", TypeName = "jsonb")]
    public string? InputRef { get; set; }

    [Required]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("timings", TypeName = "jsonb")]
    public string? Timings { get; set; }

    [Column("costs", TypeName = "jsonb")]
    public string? Costs { get; set; }

    [Column("error_info", TypeName = "jsonb")]
    public string? ErrorInfo { get; set; }

    [Column("trace_id")]
    public string? TraceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AgentId))]
    public AgentEntity Agent { get; set; } = null!;

    [ForeignKey(nameof(DepId))]
    public DeploymentEntity? Deployment { get; set; }

    [ForeignKey(nameof(NodeId))]
    public NodeEntity? Node { get; set; }
}
