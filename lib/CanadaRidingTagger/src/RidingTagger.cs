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
    /// The subnational jurisdiction is determined from the federal district's code
    /// (the authoritative source), then the provincial/territorial district is looked
    /// up in that jurisdiction's dataset. When the federal district cannot determine a
    /// jurisdiction, the jurisdiction falls back to the loaded dataset that contains the point.
    /// </summary>
    /// <param name="latitude">WGS84 latitude in degrees.</param>
    /// <param name="longitude">WGS84 longitude in degrees.</param>
    /// <returns>
    /// The matching federal and subnational districts. <see cref="RidingResult.Jurisdiction"/>
    /// is determined from the federal district's code; each district is <c>null</c> when none
    /// is loaded or the point is outside all of them.
    /// </returns>
    public RidingResult FindRidings(double latitude, double longitude)
    {
        var envelope = new Envelope(new Coordinate(longitude, latitude));

        // Federal district: first federal dataset (Jurisdiction == null) containing the point.
        District? federal = null;
        foreach (var dataset in _datasets)
        {
            if (dataset.Jurisdiction is not null)
            {
                continue;
            }
            var federalHit = FirstContaining(envelope, dataset, longitude, latitude);
            if (federalHit is not null)
            {
                federal = federalHit;
                break;
            }
        }

        // Jurisdiction: authoritative from the federal district's two-digit code prefix.
        Jurisdiction? jurisdiction = federal is null ? null : FederalJurisdiction.FromId(federal.Id);

        // Provincial/territorial district: the containing district of the jurisdiction's dataset.
        District? provincial = null;
        if (jurisdiction is not null)
        {
            provincial = FindProvincial(envelope, longitude, latitude, jurisdiction);
        }

        // Fallback: the federal district could not determine a jurisdiction (no federal
        // dataset loaded, the point lies outside it, or the code is unrecognized), so the
        // loaded dataset containing the point is the only remaining signal.
        if (jurisdiction is null)
        {
            foreach (var dataset in _datasets)
            {
                if (dataset.Jurisdiction is null)
                {
                    continue;
                }
                var hit = FirstContaining(envelope, dataset, longitude, latitude);
                if (hit is not null)
                {
                    jurisdiction = dataset.Jurisdiction;
                    provincial = hit;
                    break;
                }
            }
        }

        return new RidingResult(jurisdiction, federal, provincial);
    }
    /// <summary>
    /// Finds the federal district and the province/territory district containing the
    /// WGS84 centroid of the forward sortation area <paramref name="fsa"/>
    /// (the first three characters of a Canadian postal code; a full postal code is
    /// also accepted). Equivalent to <see cref="FindRidings(double, double)"/> at the
    /// FSA's bundled centroid.
    /// </summary>
    /// <param name="fsa">The FSA or a full postal code, case-insensitive, optional surrounding whitespace.</param>
    /// <returns>
    /// The matching federal and subnational districts, as for <see cref="FindRidings(double, double)"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fsa"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException"><paramref name="fsa"/> is not an FSA or full Canadian postal code.</exception>
    /// <exception cref="KeyNotFoundException">The FSA is not in the bundled table.</exception>
    public RidingResult FindRidingsByFsa(string fsa)
    {
        var (latitude, longitude) = FsaCentroids.Get(fsa);
        return FindRidings(latitude, longitude);
    }

    // The first district of <paramref name="dataset"/> containing the point, or null.
    // Districts tile their jurisdiction, so at most one candidate matches except exactly
    // on a shared boundary (a measure-zero ambiguity where any answer is defensible).
    private static District? FirstContaining(Envelope envelope, LoadedDataset dataset, double longitude, double latitude)
    {
        foreach (var candidate in dataset.Tree.Query(envelope))
        {
            if (PointInGeometry.Contains(longitude, latitude, candidate.Geometry))
            {
                return candidate.District;
            }
        }
        return null;
    }

    // The first district of the first loaded dataset for <paramref name="jurisdiction"/>
    // containing the point, or null when none is loaded or the point is outside all of them.
    private District? FindProvincial(Envelope envelope, double longitude, double latitude, Jurisdiction? jurisdiction)
    {
        foreach (var dataset in _datasets)
        {
            if (dataset.Jurisdiction != jurisdiction)
            {
                continue;
            }
            var hit = FirstContaining(envelope, dataset, longitude, latitude);
            if (hit is not null)
            {
                return hit;
            }
        }
        return null;
    }

    private sealed record LoadedDataset(Jurisdiction? Jurisdiction, STRtree<DistrictGeometry> Tree);
}
