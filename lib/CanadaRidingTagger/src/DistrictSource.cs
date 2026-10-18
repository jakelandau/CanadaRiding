namespace CanadaRidingTagger;

/// <summary>
/// One electoral-district dataset file (shapefile, GeoJSON, or KML) with its field definitions and jurisdiction.
/// </summary>
/// <param name="Path">
/// Path to the dataset file (.shp, .geojson, or .kml). For a shapefile the path is the .shp file;
/// its companion .prj file is read (when present) to determine the projection.
/// </param>
/// <param name="Definition">Field definitions for the dataset.</param>
/// <param name="Jurisdiction">
/// The subnational jurisdiction this dataset represents, or <c>null</c> for a federal dataset.
/// </param>
/// <param name="Year">
/// The general-election year for which this map is or was in effect, or <c>null</c> for an
/// unversioned dataset. When a jurisdiction has versioned datasets loaded, <c>null</c> datasets
/// for that jurisdiction are not the answer to current-vintage queries.
/// </param>
public sealed record DistrictSource(string Path, DatasetDefinition Definition, Jurisdiction? Jurisdiction, int? Year = null);
