using CanadaRidingTagger;
using CanadaRidingTagger.Readers;
using System.Text;

Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

string root = args.Length > 0 ? args[0] : @"D:\Documents\GitHub\CanadaRiding\data\Shapefiles";

IReadOnlyList<DistrictGeometry> ReadOk(string path, DatasetDefinition def, Jurisdiction? jur, string label)
{
    try
    {
        var r = DistrictReader.Read(new DistrictSource(path, def, jur, 0));
        Console.WriteLine($"{label}: {r.Count} districts");
        return r;
    }
    catch (Exception e)
    {
        Console.WriteLine($"{label}: FAILED {e.GetType().Name}: {e.Message}");
        return [];
    }
}

void Show(IReadOnlyCollection<string> set, string label)
{
    Console.WriteLine($"  {label} ({set.Count}):");
    foreach (var n in set.OrderBy(n => n)) Console.WriteLine($"    {n}");
}

// MB: current SHP vs 2018 KML
var mbCur = ReadOk($@"{root}\Provincial\MB\VA_MB2023_public.shp", new("EDNUM", "EDNAMEEN", "EDNAMEFR"), Jurisdiction.Manitoba, "MB current");
var mbKml = ReadOk($@"{root}\2018Manitoba.kml", new(null, "ED", "ED_French"), Jurisdiction.Manitoba, "MB 2018 KML");

var mbCurNames = mbCur.Select(d => d.District.NameEn).ToHashSet();
var mbKmlNames = mbKml.Select(d => d.District.NameEn).ToHashSet();
Console.WriteLine($"MB: current={mbCurNames.Count} kml={mbKmlNames.Count} identical={mbCurNames.SetEquals(mbKmlNames)}");
Show(mbCurNames.Except(mbKmlNames).ToHashSet(), "in current only");
Show(mbKmlNames.Except(mbCurNames).ToHashSet(), "in KML only");
// PEI: current SHP vs 2017 KML
var peiCur = ReadOk($@"{root}\Provincial\PEI\Electoral_District_Boundaries.shp", new("DIST_NO", "DISTRICT", null), Jurisdiction.PEI, "PEI current");
var peiKml = ReadOk($@"{root}\2017PEI.kml", new("DIST_NO", "DISTRICT", null), Jurisdiction.PEI, "PEI 2017 KML");

var peiCurNames = peiCur.Select(d => d.District.NameEn).ToHashSet();
var peiKmlNames = peiKml.Select(d => d.District.NameEn).ToHashSet();
Console.WriteLine($"PEI: current={peiCurNames.Count} kml={peiKmlNames.Count} identical={peiCurNames.SetEquals(peiKmlNames)}");

return 0;
