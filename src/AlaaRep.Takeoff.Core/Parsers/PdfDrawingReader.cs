using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Parsers;

public class PdfDrawingReader
{
    public DrawingDocument ReadPdf(Stream pdfStream, string fileName = "Document.pdf")
    {
        var doc = new DrawingDocument
        {
            FileName = fileName,
            FileType = "PDF"
        };

        using var pdf = PdfDocument.Open(pdfStream);
        if (pdf.NumberOfPages == 0) return doc;

        var page = pdf.GetPage(1);
        doc.Width = page.Width;
        doc.Height = page.Height;

        double pageH = page.Height;

        try
        {
            var paths = page.Paths;
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    var entity = new DrawingEntity
                    {
                        Type = DrawingEntityType.Path,
                        Layer = "PDF_Vector",
                        ColorHex = "#4A5568",
                        StrokeWidth = Math.Max(1.0, path.LineWidth)
                    };

                    foreach (var subpath in path)
                    {
                        foreach (var cmd in subpath.Commands)
                        {
                            if (cmd is PdfSubpath.Move move)
                            {
                                entity.Points.Add(new Point2D(move.Location.X, pageH - move.Location.Y));
                            }
                            else if (cmd is PdfSubpath.Line line)
                            {
                                entity.Points.Add(new Point2D(line.To.X, pageH - line.To.Y));
                            }
                        }
                    }

                    if (entity.Points.Count >= 2)
                    {
                        doc.Entities.Add(entity);
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            var words = page.GetWords();
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word.Text)) continue;

                var textEntity = new DrawingEntity
                {
                    Type = DrawingEntityType.Text,
                    Layer = "PDF_Text",
                    Text = word.Text,
                    TextSize = Math.Max(8.0, word.BoundingBox.Height),
                    ColorHex = "#1F2937"
                };
                textEntity.Points.Add(new Point2D(word.BoundingBox.Left, pageH - word.BoundingBox.Top));
                doc.Entities.Add(textEntity);
            }
        }
        catch
        {
        }

        ExtractPdfAnnotations(page, pageH, doc);

        return doc;
    }

    private void ExtractPdfAnnotations(Page page, double pageH, DrawingDocument doc)
    {
        try
        {
            var annotations = page.GetAnnotations();
            if (annotations == null) return;

            foreach (var annot in annotations)
            {
                var bbox = annot.Rectangle;
                double x1 = bbox.Left;
                double y1 = pageH - bbox.Top;
                double x2 = bbox.Right;
                double y2 = pageH - bbox.Bottom;

                var takeoff = new TakeoffObject
                {
                    Name = annot.Content ?? "PDF Markup",
                    Source = "Bluebeam/PDF",
                    ColorHex = "#EC4899",
                    StrokeWidth = 2.0
                };

                takeoff.GeometryPoints.Add(new Point2D(x1, y1));
                takeoff.GeometryPoints.Add(new Point2D(x2, y1));
                takeoff.GeometryPoints.Add(new Point2D(x2, y2));
                takeoff.GeometryPoints.Add(new Point2D(x1, y2));

                string subType = annot.Type.ToString();
                if (subType.Contains("Line", StringComparison.OrdinalIgnoreCase))
                {
                    takeoff.MeasurementType = TakeoffMeasurementType.Length;
                    takeoff.GeometryPoints = new List<Point2D> { new(x1, y1), new(x2, y2) };
                }
                else if (subType.Contains("Polygon", StringComparison.OrdinalIgnoreCase) ||
                         subType.Contains("Square", StringComparison.OrdinalIgnoreCase) ||
                         subType.Contains("Circle", StringComparison.OrdinalIgnoreCase))
                {
                    takeoff.MeasurementType = TakeoffMeasurementType.Area;
                }
                else
                {
                    takeoff.MeasurementType = TakeoffMeasurementType.Count;
                    takeoff.GeometryPoints = new List<Point2D> { new((x1 + x2) / 2, (y1 + y2) / 2) };
                }

                if (!string.IsNullOrEmpty(annot.Content))
                {
                    ParseContentProperties(annot.Content, takeoff);
                }

                doc.TakeoffObjects.Add(takeoff);
            }
        }
        catch
        {
        }
    }

    private void ParseContentProperties(string content, TakeoffObject takeoff)
    {
        var lines = content.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                string key = line.Substring(0, colonIndex).Trim();
                string val = line.Substring(colonIndex + 1).Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                {
                    takeoff.SetBimProperty(key, val);
                }
            }
            else
            {
                if (takeoff.Name == "PDF Markup")
                {
                    takeoff.Name = line.Trim();
                }
            }
        }
    }
}
