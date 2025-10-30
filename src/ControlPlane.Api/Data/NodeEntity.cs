using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlPlane.Api.Data;

[Table("nodes")]
public class NodeEntity
{
    [Key]
    [Column("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    [Column("capacity", TypeName = "jsonb")]
    public string? Capacity { get; set; }

    [Column("status", TypeName = "jsonb")]
    public string? Status { get; set; }

    [Column("heartbeat_at")]
    public DateTime HeartbeatAt { get; set; } = DateTime.UtcNow;

    public ICollection<RunEntity> Runs { get; set; } = new List<RunEntity>();
}
