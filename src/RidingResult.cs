namespace CanadaRidingTagger;

/// <summary>
/// The result of a <see cref="RidingTagger.FindRidings(double, double)"/> lookup.
/// </summary>
/// <param name="Jurisdiction">The subnational jurisdiction containing the point, or <c>null</c> when the point is outside all loaded subnational districts.</param>
/// <param name="Federal">The federal district containing the point, or <c>null</c> when no federal district is loaded or the point is outside all of them.</param>
/// <param name="Provincial">The subnational district containing the point, or <c>null</c> when none does.</param>
public sealed record RidingResult(Jurisdiction? Jurisdiction, District? Federal, District? Provincial);
