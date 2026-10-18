namespace CanadaRidingTagger;

/// <summary>
/// The result of a <see cref="RidingTagger.FindRidings(double, double, int?, int?)"/> lookup.
/// </summary>
/// <param name="Jurisdiction">The subnational jurisdiction containing the point, determined from the federal district's code, or <c>null</c> when it cannot be determined from a loaded federal district nor from a loaded subnational district containing the point.</param>
/// <param name="Federal">The federal district containing the point, or <c>null</c> when no federal district is loaded or the point is outside all of them.</param>
/// <param name="Provincial">The subnational district containing the point, or <c>null</c> when none does.</param>
public sealed record RidingResult(Jurisdiction? Jurisdiction, District? Federal, District? Provincial);
