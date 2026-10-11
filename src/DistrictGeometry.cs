namespace CanadaRidingTagger;

/// <summary>
/// One loaded electoral district: its identity plus its geometry in
/// WGS84 degrees (x = longitude, y = latitude).
/// </summary>
/// <param name="District">District identity.</param>
/// <param name="Geometry">District geometry in WGS84 degrees.</param>
internal sealed record DistrictGeometry(District District, NetTopologySuite.Geometries.Geometry Geometry);
