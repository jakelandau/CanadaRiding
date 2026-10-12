namespace CanadaRidingTagger;

/// <summary>
/// Maps a dataset's attribute fields to <see cref="District"/> identity.
/// </summary>
/// <param name="IdField">
/// The attribute field holding the district id. When <c>null</c>, the district name
/// (<see cref="NameEn"/>) is used as both the grouping key and the <see cref="District.Id"/>.
/// </param>
/// <param name="NameEn">The attribute field holding the English district name.</param>
/// <param name="NameFr">The attribute field holding the French district name, or <c>null</c> when the dataset has none.</param>
public sealed record DatasetDefinition(string? IdField, string NameEn, string? NameFr);
