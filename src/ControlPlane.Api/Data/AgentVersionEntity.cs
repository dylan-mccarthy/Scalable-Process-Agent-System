using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlPlane.Api.Data;

[Table("agent_versions")]
public class AgentVersionEntity
{
    [Key]
    [Column("version_id")]
    public int VersionId { get; set; }

    [Required]
    [Column("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [Column("version")]
    public string Version { get; set; } = string.Empty;

    [Column("spec", TypeName = "jsonb")]
    public string? Spec { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AgentId))]
    public AgentEntity Agent { get; set; } = null!;
}
