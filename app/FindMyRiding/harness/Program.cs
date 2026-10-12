using CanadaRidingTagger;

string dataRoot = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "Shapefiles");
// Default coordinate = the goal's test point (Ottawa). Pass "<dataRoot> <lat> <lon>" to query another point.
double lat = 45.4215, lon = -75.6972;
bool query = false;
if (args.Length >= 3 && double.TryParse(args[1], out double a) && double.TryParse(args[2], out double b))
{
    lat = a;
    lon = b;
    query = true;
}

var tagger = new RidingTagger();
foreach (var source in StandardDatasets.All(dataRoot))
    tagger.Load(source);

RidingResult r = tagger.FindRidings(latitude: lat, longitude: lon);
Console.WriteLine($"FEDERAL={r.Federal?.NameEn ?? "<none>"}");
Console.WriteLine($"PROVINCIAL={r.Provincial?.NameEn ?? "<none>"}");
Console.WriteLine($"JURISDICTION={r.Jurisdiction?.ToString() ?? "<none>"}");

// Goal assertion (default/test coordinate): the federal riding must be an Ottawa riding.
if (!query && (r.Federal is null || !r.Federal.NameEn.StartsWith("Ottawa", StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine($"ASSERT FAILED: expected an Ottawa federal riding, got: {r.Federal?.NameEn ?? "<none>"}");
    Environment.Exit(1);
}
if (!query)
    Console.WriteLine("ASSERT PASSED: federal riding is Ottawa");
