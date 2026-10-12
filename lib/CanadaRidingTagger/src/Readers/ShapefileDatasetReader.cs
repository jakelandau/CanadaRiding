using System.Text;
using CanadaRidingTagger;
using CanadaRidingTagger.Projections;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;

namespace CanadaRidingTagger.Readers;

/// <summary>
/// Reads a shapefile dataset. Coordinates are inverse-projected through the
/// companion <c>.prj</c> (when present) to WGS84 degrees; without a
/// <c>.prj</c> the file is assumed to be in WGS84 degrees.
/// </summary>
internal static class ShapefileDatasetReader
{
    public static IReadOnlyList<DistrictGeometry> Read(DistrictSource source)
    {
        var factory = new GeometryFactory();
        var result = new List<DistrictGeometry>();

        using var reader = new ShapefileDataReader(source.Path, factory, Encoding.UTF8);

        // Field index by name (field 0 is the shape column).
        var fields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < reader.FieldCount; i++)
        {
            fields[reader.GetName(i)] = i;
        }

        string? PrjPath = Path.ChangeExtension(source.Path, ".prj");
        IProjection? projection = File.Exists(PrjPath)
            ? ProjectionFactory.Create(File.ReadAllText(PrjPath))
            : null;
        var projector = projection is null ? null : new InverseProjector(projection);

        while (reader.Read())
        {
            Geometry geometry = projector is null
                ? reader.Geometry
                : projector.Transform(reader.Geometry);

            string? Attr(string field)
            {
                return fields.TryGetValue(field, out int index)
                    ? reader.GetValue(index) is object v && v.ToString() is string s ? s : null
                    : null;
            }

            District district = DistrictFactory.Extract(source.Definition, Attr, null);
            result.Add(new DistrictGeometry(district, geometry));
        }

        return result;
    }

    internal sealed class InverseProjector : GeometryTransformer
    {
        private readonly IProjection _projection;

        public InverseProjector(IProjection projection)
        {
            _projection = projection;
        }

        protected override CoordinateSequence TransformCoordinates(CoordinateSequence coords, Geometry parent)
        {
            var src = new Coordinate[coords.Count];
            for (int i = 0; i < coords.Count; i++)
            {
                src[i] = coords.GetCoordinate(i);
            }
            for (int i = 0; i < src.Length; i++)
            {
                _projection.Inverse(src[i].X, src[i].Y, out double lat, out double lon);
                src[i] = new Coordinate(lon * 180.0 / Math.PI, lat * 180.0 / Math.PI);
            }
            return CreateCoordinateSequence(src);
        }
    }
}
