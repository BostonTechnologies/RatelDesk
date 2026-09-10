using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Helpdesk.Infrastructure.Persistence;

public static class VectorExtensions
{
    public static PropertyBuilder<Vector> HasVectorType(this PropertyBuilder<Vector> builder, int dimensions)
    {
        return builder.HasColumnType($"vector({dimensions})");
    }
}
