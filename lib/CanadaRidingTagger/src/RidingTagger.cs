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
        _datasets.Add(new LoadedDataset(source.Jurisdiction, source.Year, tree));
    }

    /// <summary>
    /// The current vintage for <paramref name="jurisdiction"/> (or the federal
    /// jurisdiction when <paramref name="jurisdiction"/> is <c>null</c>): the
    /// maximum year among the jurisdiction's loaded versioned datasets.
    /// </summary>
    /// <param name="jurisdiction">A province/territory, or <c>null</c> for the federal jurisdiction.</param>
    /// <returns>The maximum loaded year, or <c>null</c> when no loaded dataset for the jurisdiction carries a year.</returns>
    public int? CurrentYear(Jurisdiction? jurisdiction)
    {
        int? current = null;
        foreach (var dataset in _datasets)
        {
            if (dataset.Jurisdiction == jurisdiction && dataset.Year is not null &&
                (current is null || dataset.Year > current))
            {
                current = dataset.Year;
            }
        }
        return current;
    }

    /// <summary>
    /// The distinct years of the loaded versioned datasets for <paramref name="jurisdiction"/>
    /// (or the federal jurisdiction when <paramref name="jurisdiction"/> is <c>null</c>), ascending.
    /// </summary>
    /// <param name="jurisdiction">A province/territory, or <c>null</c> for the federal jurisdiction.</param>
    /// <returns>The loaded years in ascending order; empty when no loaded dataset for the jurisdiction carries a year.</returns>
    public IReadOnlyList<int> Vintages(Jurisdiction? jurisdiction)
    {
        var years = new SortedSet<int>();
        foreach (var dataset in _datasets)
        {
            if (dataset.Jurisdiction == jurisdiction && dataset.Year is not null)
            {
                years.Add(dataset.Year.Value);
            }
        }
        return years.ToArray();
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
    /// <param name="federalYear">
    /// The federal map vintage to answer from, or <c>null</c> for the current one (the
    /// most recent loaded federal vintage). An explicit year must be loaded.
    /// </param>
    /// <param name="provincialYear">
    /// The provincial/territorial map vintage to answer from, or <c>null</c> for the
    /// current one (the most recent loaded vintage of the point's jurisdiction).
    /// An explicit year must be loaded for the point's jurisdiction.
    /// </param>
    /// <returns>
    /// The matching federal and subnational districts. <see cref="RidingResult.Jurisdiction"/>
    /// is determined from the federal district's code; each district is <c>null</c> when none
    /// is loaded or the point is outside all of them.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// An explicit year was given for which no dataset of that jurisdiction is loaded.
    /// </exception>
    public RidingResult FindRidings(double latitude, double longitude, int? federalYear = null, int? provincialYear = null)
    {
        var envelope = new Envelope(new Coordinate(longitude, latitude));

        // Federal district: first dataset of the selected federal vintage containing the point.
        var federalDatasets = DatasetsFor(null, federalYear);
        if (federalYear is not null && federalDatasets.Count == 0)
        {
            throw new ArgumentException($"No federal dataset loaded for year {federalYear}.", nameof(federalYear));
        }
        District? federal = null;
        foreach (var dataset in federalDatasets)
        {
            var federalHit = FirstContaining(envelope, dataset, longitude, latitude);
            if (federalHit is not null)
            {
                federal = federalHit;
                break;
            }
        }

        // Jurisdiction: authoritative from the federal district's two-digit code prefix.
        Jurisdiction? jurisdiction = federal is null ? null : FederalJurisdiction.FromId(federal.Id);

        // Provincial/territorial district: the containing district of the jurisdiction's
        // selected-vintage datasets.
        District? provincial = null;
        if (jurisdiction is not null)
        {
            var provincialDatasets = DatasetsFor(jurisdiction, provincialYear);
            if (provincialYear is not null && provincialDatasets.Count == 0)
            {
                throw new ArgumentException($"No {jurisdiction} dataset loaded for year {provincialYear}.", nameof(provincialYear));
            }
            provincial = FirstHit(provincialDatasets, envelope, longitude, latitude);
        }

        // Fallback: the federal district could not determine a jurisdiction (no federal
        // dataset loaded, the point lies outside it, or the code is unrecognized), so the
        // loaded dataset containing the point is the only remaining signal. Jurisdictions
        // are probed in load order, each at the selected vintage; a jurisdiction with no
        // dataset for the selected vintage cannot contain the point at that vintage.
        if (jurisdiction is null)
        {
            foreach (var candidate in _datasets
                         .Select(d => d.Jurisdiction)
                         .Where(j => j is not null)
                         .Distinct())
            {
                var hit = FirstHit(DatasetsFor(candidate, provincialYear), envelope, longitude, latitude);
                if (hit is not null)
                {
                    jurisdiction = candidate;
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
    /// also accepted). Equivalent to <see cref="FindRidings(double, double, int?, int?)"/> at the
    /// FSA's bundled centroid.
    /// </summary>
    /// <param name="fsa">The FSA or a full postal code, case-insensitive, optional surrounding whitespace.</param>
    /// <param name="federalYear">The federal map vintage to answer from, or <c>null</c> for the current one.</param>
    /// <param name="provincialYear">The provincial/territorial map vintage to answer from, or <c>null</c> for the current one.</param>
    /// <returns>
    /// The matching federal and subnational districts, as for <see cref="FindRidings(double, double, int?, int?)"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fsa"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An explicit year was given for which no dataset of that jurisdiction is loaded.</exception>
    /// <exception cref="FormatException"><paramref name="fsa"/> is not an FSA or full Canadian postal code.</exception>
    /// <exception cref="KeyNotFoundException">The FSA is not in the bundled table.</exception>
    public RidingResult FindRidingsByFsa(string fsa, int? federalYear = null, int? provincialYear = null)
    {
        var (latitude, longitude) = FsaCentroids.Get(fsa);
        return FindRidings(latitude, longitude, federalYear, provincialYear);
    }

    // The loaded datasets for <paramref name="jurisdiction"/> (null = the federal
    // jurisdiction) at the selected vintage: the explicit year's datasets when a year
    // is given, otherwise the jurisdiction's current vintage (its maximum loaded year)
    // when at least one of its datasets is versioned, otherwise all of its datasets.
    // Load order is preserved.
    private List<LoadedDataset> DatasetsFor(Jurisdiction? jurisdiction, int? year)
    {
        int? current = year is null ? CurrentYear(jurisdiction) : null;
        var result = new List<LoadedDataset>();
        foreach (var dataset in _datasets)
        {
            if (dataset.Jurisdiction != jurisdiction)
            {
                continue;
            }
            if (year is null ? current is null || dataset.Year == current : dataset.Year == year)
            {
                result.Add(dataset);
            }
        }
        return result;
    }

    // The first district of the listed datasets, in load order, containing the point, or null.
    // Districts tile their jurisdiction, so at most one candidate matches except exactly
    // on a shared boundary (a measure-zero ambiguity where any answer is defensible).
    private static District? FirstHit(List<LoadedDataset> datasets, Envelope envelope, double longitude, double latitude)
    {
        foreach (var dataset in datasets)
        {
            var hit = FirstContaining(envelope, dataset, longitude, latitude);
            if (hit is not null)
            {
                return hit;
            }
        }
        return null;
    }

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

    private sealed record LoadedDataset(Jurisdiction? Jurisdiction, int? Year, STRtree<DistrictGeometry> Tree);
}
