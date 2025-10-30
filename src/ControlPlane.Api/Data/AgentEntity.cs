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

    [Required]
    [Column("instructions", TypeName = "text")]
    public string Instructions { get; set; } = string.Empty;

    [Column("model_profile", TypeName = "jsonb")]
    public string? ModelProfile { get; set; }

    public ICollection<AgentVersionEntity> Versions { get; set; } = new List<AgentVersionEntity>();
    public ICollection<DeploymentEntity> Deployments { get; set; } = new List<DeploymentEntity>();
    public ICollection<RunEntity> Runs { get; set; } = new List<RunEntity>();
}
