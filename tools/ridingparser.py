# /// script
# requires-python = ">=3.13"
# dependencies = [
#     "xmltodict",
# ]
# ///
import xmltodict
import json
with open("2021Yukon.kml", 'r') as f:
    xml = f.read()
    print()

ridingList = []
i = 1
for riding in xmltodict.parse(xml)["kml"]["Document"]["Folder"]["Placemark"]:
    ridingID = i
    i += 1
    ridingNameEN = riding["ExtendedData"]["SchemaData"]["SimpleData"][1]["#text"]
    ridingNameFR = riding["ExtendedData"]["SchemaData"]["SimpleData"][2]["#text"]
    match ridingID:
        case "29":
            ridingList.append({"Id": ridingID, "EN": ridingNameEN, "FR": ridingNameFR, "Geo": [shape for shape in riding["MultiGeometry"]["Polygon"]]}) 
        case _:
            ridingList.append({
                "Id": ridingID,
                "EN": ridingNameEN,
                "FR": ridingNameFR,
                "Geo": [riding["Polygon"]]
            })
            
with open("pruned_ridings.json",'w') as f:
    f.write(json.dumps(ridingList, indent=4))