// Historical EF migration snapshots reference Pgvector.Vector. This isolated compatibility
// type lets those immutable snapshots compile after rc.7 removes the pgvector package.
namespace Pgvector;

[Obsolete("Only used by historical EF migration snapshots; not mapped by the runtime model.")]
public readonly struct Vector(float[] values)
{
    private readonly float[] values = values;

    public float[] ToArray() => values ?? [];
}
