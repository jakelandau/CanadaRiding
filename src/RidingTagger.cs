using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace CanadaRidingTagger;

/// <summary>
/// Loads electoral-district datasets and answers, for a point in WGS84,
/// which federal riding it falls in and which province or territory contains it.
/// </summary>
public sealed class RidingTagger
{
    private readonly List<LoadedDataset> _datasets = [];
    private static readonly GeometryFactory GeometryFactory = new();

    /// <summary>
    /// Reads <paramref name="source"/> and adds it to the tagger, building a spatial index over its districts.
    /// </summary>
    /// <param name="source">The dataset to load.</param>
    /// <exception cref="FileNotFoundException">The dataset file does not exist.</exception>
    /// <exception cref="FormatException">The extension is unsupported, or a required field is missing.</exception>
    public void Load(DistrictSource source)
    {
        var geometries = Readers.DistrictReader.Read(source);
        var tree = new STRtree<DistrictGeometry>();
        foreach (var district in geometries)
        {
            tree.Insert(district.Geometry.EnvelopeInternal, district);
        }
        _datasets.Add(new LoadedDataset(source.Jurisdiction, tree));
    }

    /// <summary>
    /// Finds the federal district and the province/territory district containing the point.
    /// </summary>
    /// <param name="latitude">WGS84 latitude in degrees.</param>
    /// <param name="longitude">WGS84 longitude in degrees.</param>
    /// <returns>The matching federal and subnational districts, each <c>null</c> when none is loaded or the point is outside all of them.</returns>
    public RidingResult FindRidings(double latitude, double longitude)
    {
        var point = GeometryFactory.CreatePoint(new Coordinate(longitude, latitude));
        var envelope = point.EnvelopeInternal;
        District? federal = null, provincial = null;
        Jurisdiction? jurisdiction = null;

        foreach (var dataset in _datasets)
        {
            foreach (var candidate in dataset.Tree.Query(envelope))
            {
                if (!candidate.Geometry.Covers(point))
                {
                    continue;
                }
                if (dataset.Jurisdiction is null)
                {
                    federal = candidate.District;
                }
                else
                {
                    provincial = candidate.District;
                    jurisdiction = dataset.Jurisdiction;
                }
            }
        }

        return new RidingResult(jurisdiction, federal, provincial);
    }

    private sealed record LoadedDataset(Jurisdiction? Jurisdiction, STRtree<DistrictGeometry> Tree);
}
