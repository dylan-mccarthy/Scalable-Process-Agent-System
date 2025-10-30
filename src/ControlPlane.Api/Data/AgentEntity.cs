using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlPlane.Api.Data;

[Table("agents")]
public class AgentEntity
{
    [Key]
    [Column("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("instructions", TypeName = "text")]
    public string Instructions { get; set; } = string.Empty;

    [Column("model_profile", TypeName = "jsonb")]
    public string? ModelProfile { get; set; }

    [Column("budget", TypeName = "jsonb")]
    public string? Budget { get; set; }

    [Column("tools", TypeName = "jsonb")]
    public string? Tools { get; set; }

    [Column("input_connector", TypeName = "jsonb")]
    public string? InputConnector { get; set; }

    [Column("output_connector", TypeName = "jsonb")]
    public string? OutputConnector { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    public ICollection<AgentVersionEntity> Versions { get; set; } = new List<AgentVersionEntity>();
    public ICollection<DeploymentEntity> Deployments { get; set; } = new List<DeploymentEntity>();
    public ICollection<RunEntity> Runs { get; set; } = new List<RunEntity>();
}
