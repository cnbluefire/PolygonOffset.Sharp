#pragma warning disable IDE1006

using System.Numerics;

namespace PolygonOffset;

public static class PolygonOffsetHelper
{
    // translate from 
    // https://github.com/Stanko/offset-polygon

    // TODO check these comments:
    // Assuming that polygon vertices are in clockwise order

    private class Edge : OffsetEdge
    {
        public int index { get; set; }

        public Vector2 inwardNormal { get; set; }

        public Vector2 outwardNormal { get; set; }
    }

    private class OffsetEdge
    {
        public Vector2 vertex1 { get; set; }

        public Vector2 vertex2 { get; set; }
    }

    private class Polygon
    {
        public List<Edge>? edges { get; set; }

        public List<OffsetEdge>? offsetEdges { get; set; }

        public float maxX { get; set; }

        public float maxY { get; set; }

        public float minX { get; set; }

        public float minY { get; set; }

        public List<Vector2>? vertices { get; set; }
    }

    private const float TWO_PI = MathF.PI * 2;

    // See http://paulbourke.net/geometry/pointlineplane/
    private static Vector2 inwardEdgeNormal(Vector2 vertex1, Vector2 vertex2)
    {
        // Assuming that polygon vertices are in clockwise order
        var dx = vertex2.X - vertex1.X;
        var dy = vertex2.Y - vertex1.Y;
        var edgeLength = MathF.Sqrt(dx * dx + dy * dy);

        return new Vector2(
          -dy / edgeLength,
          dx / edgeLength
        );
    }

    private static Vector2 outwardEdgeNormal(Vector2 vertex1, Vector2 vertex2)
    {
        var n = inwardEdgeNormal(vertex1, vertex2);

        return new Vector2(
          -n.X,
          -n.Y
        );
    }

    private static Polygon createPolygon(List<Vector2> vertices)
    {
        var edges = new List<Edge>();
        var minX = vertices.Count > 0 ? vertices[0].X : float.NaN;
        var minY = vertices.Count > 0 ? vertices[0].Y : float.NaN;
        var maxX = minX;
        var maxY = minY;

        for (var i = 0; i < vertices.Count; i++)
        {
            var vertex1 = vertices[i];
            var vertex2 = vertices[(i + 1) % vertices.Count];

            var outwardNormal = outwardEdgeNormal(vertex1, vertex2);

            var inwardNormal = inwardEdgeNormal(vertex1, vertex2);

            var edge = new Edge()
            {
                vertex1 = vertex1,
                vertex2 = vertex2,
                index = i,
                outwardNormal = outwardNormal,
                inwardNormal = inwardNormal,
            };

            edges.Add(edge);

            var x = vertices[i].X;
            var y = vertices[i].Y;
            minX = MathF.Min(x, minX);
            minY = MathF.Min(y, minY);
            maxX = MathF.Max(x, maxX);
            maxY = MathF.Max(y, maxY);
        }

        var polygon = new Polygon()
        {
            vertices = vertices,
            edges = edges,
            minX = minX,
            minY = minY,
            maxX = maxX,
            maxY = maxY,
        };

        return polygon;
    }

    // based on http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline2d/, edgeA => "line a", edgeB => "line b"

    private static (float x, float y, bool isIntersectionOutside)? edgesIntersection(OffsetEdge edgeA, OffsetEdge edgeB)
    {
        var den =
          (edgeB.vertex2.Y - edgeB.vertex1.Y) * (edgeA.vertex2.X - edgeA.vertex1.X) -
          (edgeB.vertex2.X - edgeB.vertex1.X) * (edgeA.vertex2.Y - edgeA.vertex1.Y);

        if (den == 0)
        {
            return null; // lines are parallel or coincident
        }

        var ua =
          ((edgeB.vertex2.X - edgeB.vertex1.X) * (edgeA.vertex1.Y - edgeB.vertex1.Y) -
            (edgeB.vertex2.Y - edgeB.vertex1.Y) *
              (edgeA.vertex1.X - edgeB.vertex1.X)) /
          den;

        var ub =
          ((edgeA.vertex2.X - edgeA.vertex1.X) * (edgeA.vertex1.Y - edgeB.vertex1.Y) -
            (edgeA.vertex2.Y - edgeA.vertex1.Y) *
              (edgeA.vertex1.X - edgeB.vertex1.X)) /
          den;

        // Edges are not intersecting but the lines defined by them are
        var isIntersectionOutside = ua < 0 || ub < 0 || ua > 1 || ub > 1;

        return (
          x: edgeA.vertex1.X + ua * (edgeA.vertex2.X - edgeA.vertex1.X),
          y: edgeA.vertex1.Y + ua * (edgeA.vertex2.Y - edgeA.vertex1.Y),
          isIntersectionOutside
        );
    }

    private static void appendArc(
          int arcSegments,
          List<Vector2> vertices,
          Vector2 center,
          float radius,
          Vector2 startVertex,
          Vector2 endVertex,
          bool isPaddingBoundary
        )
    {
        var startAngle = (float)MathF.Atan2(
          startVertex.Y - center.Y,
          startVertex.X - center.X
        );
        var endAngle = MathF.Atan2(endVertex.Y - center.Y, endVertex.X - center.X);

        if (startAngle < 0)
        {
            startAngle += TWO_PI;
        }

        if (endAngle < 0)
        {
            endAngle += TWO_PI;
        }

        var angle =
          startAngle > endAngle
            ? startAngle - endAngle
            : startAngle + TWO_PI - endAngle;
        var angleStep = (isPaddingBoundary ? -angle : TWO_PI - angle) / arcSegments;

        vertices.Add(startVertex);

        for (var i = 1; i < arcSegments; ++i)
        {
            var angle2 = startAngle + angleStep * i;

            var vertex = new Vector2(
              x: center.X + MathF.Cos(angle2) * radius,
              y: center.Y + MathF.Sin(angle2) * radius
            );

            vertices.Add(vertex);
        }

        vertices.Add(endVertex);
    }

    private static OffsetEdge createOffsetEdge(Edge edge, float dx, float dy)
    {
        return new OffsetEdge()
        {
            vertex1 = new Vector2(
            edge.vertex1.X + dx,
            edge.vertex1.Y + dy
          ),
            vertex2 = new Vector2(
            edge.vertex2.X + dx,
            edge.vertex2.Y + dy
          ),
        };
    }

    private static Polygon createMarginPolygon(
          Polygon polygon,
          float offset,
          int arcSegments
        )
    {
        ArgumentNullException.ThrowIfNull(polygon.edges);

        var offsetEdges = new List<OffsetEdge>();

        for (var i = 0; i < polygon.edges.Count; i++)
        {
            var edge = polygon.edges[i];
            var dx = edge.outwardNormal.X * offset;
            var dy = edge.outwardNormal.Y * offset;
            offsetEdges.Add(createOffsetEdge(edge, dx, dy));
        }

        var vertices = new List<Vector2>();

        for (var i = 0; i < offsetEdges.Count; i++)
        {
            var thisEdge = offsetEdges[i];
            var prevEdge =
              offsetEdges[(i + offsetEdges.Count - 1) % offsetEdges.Count];
            var vertex = edgesIntersection(prevEdge, thisEdge);
            if (vertex.HasValue && (!vertex.Value.isIntersectionOutside || arcSegments < 1))
            {
                vertices.Add(new Vector2(vertex.Value.x, vertex.Value.y));
            }
            else
            {
                var arcCenter = polygon.edges[i].vertex1;

                appendArc(
                  arcSegments,
                  vertices,
                  arcCenter,
                  offset,
                  prevEdge.vertex2,
                  thisEdge.vertex1,
                  false
                );
            }
        }

        var marginPolygon = createPolygon(vertices);

        marginPolygon.offsetEdges = offsetEdges;

        return marginPolygon;
    }

    private static Polygon createPaddingPolygon(
          Polygon polygon,
          float offset,
          int arcSegments
        )
    {
        ArgumentNullException.ThrowIfNull(polygon.edges);

        var offsetEdges = new List<OffsetEdge>();

        for (var i = 0; i < polygon.edges.Count; i++)
        {
            var edge = polygon.edges[i];
            var dx = edge.inwardNormal.X * offset;
            var dy = edge.inwardNormal.Y * offset;
            offsetEdges.Add(createOffsetEdge(edge, dx, dy));
        }

        var vertices = new List<Vector2>();

        for (var i = 0; i < offsetEdges.Count; i++)
        {
            var thisEdge = offsetEdges[i];
            var prevEdge =
              offsetEdges[(i + offsetEdges.Count - 1) % offsetEdges.Count];
            var vertex = edgesIntersection(prevEdge, thisEdge);
            if (vertex.HasValue && (!vertex.Value.isIntersectionOutside || arcSegments < 1))
            {
                vertices.Add(new Vector2(vertex.Value.x, vertex.Value.y));
            }
            else
            {
                var arcCenter = polygon.edges[i].vertex1;

                appendArc(
                  arcSegments,
                  vertices,
                  arcCenter,
                  offset,
                  prevEdge.vertex2,
                  thisEdge.vertex1,
                  true
                );
            }
        }

        var paddingPolygon = createPolygon(vertices);

        paddingPolygon.offsetEdges = offsetEdges;

        return paddingPolygon;
    }

    public static List<Vector2>? OffsetPolygon(
          List<Vector2> vertices,
          float offset,
          int arcSegments = 0
        )
    {
        var polygon = createPolygon(vertices);

        if (offset > 0)
        {
            return createMarginPolygon(polygon, offset, arcSegments).vertices;
        }
        else
        {
            return createPaddingPolygon(polygon, -offset, arcSegments).vertices;
        }
    }
}
