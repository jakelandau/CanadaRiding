namespace CanadaRidingTagger;

/// <summary>
/// An electoral district identified by a point-in-polygon lookup.
/// </summary>
/// <param name="Id">The district identifier from the dataset (id-field value, or the district name when the dataset has no id field).</param>
/// <param name="NameEn">The English district name.</param>
/// <param name="NameFr">The French district name, or <c>null</c> when the dataset has no French name field (or the value is empty).</param>
public sealed record District(string Id, string NameEn, string? NameFr);
