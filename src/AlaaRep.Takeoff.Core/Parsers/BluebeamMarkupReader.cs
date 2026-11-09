using System.Text.Json;
using System.Xml.Linq;
using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Parsers;

public class BluebeamMarkupReader
{
    public List<TakeoffObject> ReadMarkups(Stream stream, string fileName = "markups.csv")
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();

        if (ext == ".json")
        {
            return ReadJsonMarkups(content);
        }
        else if (ext == ".xml" || ext == ".btx")
        {
            return ReadXmlMarkups(content);
        }
        else
        {
            return ReadCsvMarkups(content);
        }
    }

    public List<TakeoffObject> ReadCsvMarkups(string csvContent)
    {
        var result = new List<TakeoffObject>();
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return result;

        var headers = ParseCsvLine(lines[0]);
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            headerMap[headers[i].Trim()] = i;
        }

        for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
        {
            var cols = ParseCsvLine(lines[lineIdx]);
            if (cols.Count == 0 || cols.All(string.IsNullOrWhiteSpace)) continue;

            string GetCol(params string[] candidateNames)
            {
                foreach (var name in candidateNames)
                {
                    if (headerMap.TryGetValue(name, out int idx) && idx < cols.Count)
                    {
                        return cols[idx].Trim();
                    }
                }
                return string.Empty;
            }

            string subject = GetCol("Subject", "Type", "Tool");
            string label = GetCol("Label", "Mark", "Name", "Comments");
            string zone = GetCol("Zone", "Space", "Location", "Room");
            string layer = GetCol("Layer", "Category");

            var takeoff = new TakeoffObject
            {
                Name = !string.IsNullOrEmpty(label) ? label : (!string.IsNullOrEmpty(subject) ? subject : $"Item {lineIdx}"),
                BluebeamSubject = subject,
                BluebeamLabel = label,
                Category = !string.IsNullOrEmpty(layer) ? layer : (!string.IsNullOrEmpty(subject) ? subject : "General"),
                Source = "Bluebeam CSV",
                ColorHex = GetColorForSubject(subject)
            };

            if (headerMap.ContainsKey("Area") && !string.IsNullOrEmpty(GetCol("Area")))
            {
                takeoff.MeasurementType = TakeoffMeasurementType.Area;
            }
            else if (headerMap.ContainsKey("Length") && !string.IsNullOrEmpty(GetCol("Length")))
            {
                takeoff.MeasurementType = TakeoffMeasurementType.Length;
            }
            else if (subject.Contains("Area", StringComparison.OrdinalIgnoreCase) || subject.Contains("Polygon", StringComparison.OrdinalIgnoreCase))
            {
                takeoff.MeasurementType = TakeoffMeasurementType.Area;
            }
            else if (subject.Contains("Length", StringComparison.OrdinalIgnoreCase) || subject.Contains("Polyline", StringComparison.OrdinalIgnoreCase) || subject.Contains("Perimeter", StringComparison.OrdinalIgnoreCase))
            {
                takeoff.MeasurementType = TakeoffMeasurementType.Length;
            }
            else
            {
                takeoff.MeasurementType = TakeoffMeasurementType.Count;
            }

            foreach (var kvp in headerMap)
            {
                int colIdx = kvp.Value;
                if (colIdx < cols.Count)
                {
                    string key = kvp.Key;
                    string val = cols[colIdx].Trim();
                    if (!string.IsNullOrEmpty(val))
                    {
                        takeoff.SetBimProperty(key, val);
                    }
                }
            }

            if (!string.IsNullOrEmpty(zone))
            {
                takeoff.ZoneId = zone;
                takeoff.SetBimProperty("Zone", zone);
            }

            string coords = GetCol("Points", "Coordinates", "Location");
            if (!string.IsNullOrEmpty(coords))
            {
                var parsedPts = ParsePointsString(coords);
                if (parsedPts.Count > 0)
                {
                    takeoff.GeometryPoints = parsedPts;
                }
            }

            result.Add(takeoff);
        }

        return result;
    }

    public List<TakeoffObject> ReadXmlMarkups(string xmlContent)
    {
        var result = new List<TakeoffObject>();
        try
        {
            var xdoc = XDocument.Parse(xmlContent);
            var markups = xdoc.Descendants().Where(e =>
                e.Name.LocalName.Equals("Markup", StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.Equals("Annotation", StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.Equals("Tool", StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase));

            foreach (var m in markups)
            {
                string subject = m.Attribute("Subject")?.Value ?? m.Element("Subject")?.Value ?? string.Empty;
                string label = m.Attribute("Label")?.Value ?? m.Element("Label")?.Value ?? string.Empty;
                string typeStr = m.Attribute("Type")?.Value ?? m.Element("Type")?.Value ?? string.Empty;
                string space = m.Attribute("Space")?.Value ?? m.Element("Space")?.Value ?? string.Empty;

                var takeoff = new TakeoffObject
                {
                    Name = !string.IsNullOrEmpty(label) ? label : (!string.IsNullOrEmpty(subject) ? subject : "XML Markup"),
                    Category = !string.IsNullOrEmpty(subject) ? subject : "General",
                    Source = "Bluebeam XML",
                    BluebeamSubject = subject,
                    BluebeamLabel = label,
                    ColorHex = GetColorForSubject(subject)
                };

                if (typeStr.Contains("Area", StringComparison.OrdinalIgnoreCase) || typeStr.Contains("Polygon", StringComparison.OrdinalIgnoreCase))
                {
                    takeoff.MeasurementType = TakeoffMeasurementType.Area;
                }
                else if (typeStr.Contains("Length", StringComparison.OrdinalIgnoreCase) || typeStr.Contains("Line", StringComparison.OrdinalIgnoreCase))
                {
                    takeoff.MeasurementType = TakeoffMeasurementType.Length;
                }
                else
                {
                    takeoff.MeasurementType = TakeoffMeasurementType.Count;
                }

                if (!string.IsNullOrEmpty(space))
                {
                    takeoff.ZoneId = space;
                    takeoff.SetBimProperty("Zone", space);
                }

                foreach (var attr in m.Attributes())
                {
                    takeoff.SetBimProperty(attr.Name.LocalName, attr.Value);
                }
                foreach (var elem in m.Elements().Where(e => !e.HasElements))
                {
                    takeoff.SetBimProperty(elem.Name.LocalName, elem.Value);
                }

                var pointsElem = m.Element("Points") ?? m.Element("Vertices");
                if (pointsElem != null)
                {
                    var pts = ParsePointsString(pointsElem.Value);
                    if (pts.Count > 0) takeoff.GeometryPoints = pts;
                }

                result.Add(takeoff);
            }
        }
        catch
        {
        }

        return result;
    }

    public List<TakeoffObject> ReadJsonMarkups(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<TakeoffObject>>(jsonContent, options);
            if (list != null) return list;
        }
        catch
        {
        }
        return new List<TakeoffObject>();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private static List<Point2D> ParsePointsString(string pointsStr)
    {
        var result = new List<Point2D>();
        var parts = pointsStr.Split(new[] { ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var coords = part.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (coords.Length == 2 &&
                double.TryParse(coords[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(coords[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y))
            {
                result.Add(new Point2D(x, y));
            }
        }
        return result;
    }

    private static string GetColorForSubject(string subject)
    {
        if (string.IsNullOrEmpty(subject)) return "#3B82F6";
        int hash = Math.Abs(subject.GetHashCode());
        string[] palette = { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16" };
        return palette[hash % palette.Length];
    }
}
