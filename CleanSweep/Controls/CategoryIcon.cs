using System;
using System.Windows;
using System.Windows.Media;

namespace CleanSweep.Controls;

public class CategoryIcon : FrameworkElement
{
    private const double IconSize = 34.0;

    public static readonly DependencyProperty CategoryKeyProperty =
        DependencyProperty.Register(
            nameof(CategoryKey),
            typeof(string),
            typeof(CategoryIcon),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public string CategoryKey
    {
        get => (string)GetValue(CategoryKeyProperty);
        set => SetValue(CategoryKeyProperty, value);
    }

    public CategoryIcon()
    {
        Width = IconSize;
        Height = IconSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // 1. Tinted rounded background tile (#43C6AC with 10% opacity)
        var tileBrush = new SolidColorBrush(Color.FromArgb(26, 0x43, 0xC6, 0xAC));
        tileBrush.Freeze();
        dc.DrawRoundedRectangle(tileBrush, null, new Rect(0, 0, IconSize, IconSize), 9, 9);

        // 2. Icon glyph pen (#E7E9EE, thickness 1.6, round caps)
        var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E7E9EE")), 1.6)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        switch (CategoryKey?.ToLowerInvariant())
        {
            case "temp":
                DrawTemp(dc, pen);
                break;
            case "browser_cache":
            case "cache":
                DrawBrowserCache(dc, pen);
                break;
            case "thumbnails":
                DrawThumbnails(dc, pen);
                break;
            case "crash_reports":
                DrawCrashReports(dc, pen);
                break;
            case "recycle_bin":
                DrawRecycleBin(dc, pen);
                break;
            case "memory_dumps":
                DrawMemoryDumps(dc, pen);
                break;
            case "recent_items":
                DrawRecentItems(dc, pen);
                break;
            case "incomplete_downloads":
                DrawIncompleteDownloads(dc, pen);
                break;
            case "shader_cache":
                DrawShaderCache(dc, pen);
                break;
            default:
                DrawGeneric(dc, pen);
                break;
        }
    }

    private void DrawGeneric(DrawingContext dc, Pen pen)
    {
        dc.DrawRoundedRectangle(null, pen, new Rect(11, 9, 12, 16), 2, 2);
    }

    private void DrawTemp(DrawingContext dc, Pen pen)
    {
        dc.DrawRoundedRectangle(null, pen, new Rect(11, 8, 12, 17), 1.5, 1.5);
        dc.DrawLine(pen, new Point(14, 15), new Point(20, 15));
        dc.DrawLine(pen, new Point(14, 19), new Point(20, 19));
    }

    private void DrawBrowserCache(DrawingContext dc, Pen pen)
    {
        Point center = new Point(17, 17);
        dc.DrawEllipse(null, pen, center, 9, 9);
        dc.DrawLine(pen, new Point(8, 17), new Point(26, 17));
        dc.DrawEllipse(null, pen, center, 4, 9);
    }

    private void DrawThumbnails(DrawingContext dc, Pen pen)
    {
        dc.DrawRoundedRectangle(null, pen, new Rect(8, 9, 18, 16), 2, 2);
        dc.DrawEllipse(null, pen, new Point(12.5, 14), 1.6, 1.6);

        var pathGeo = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(10, 22), IsClosed = false };
        figure.Segments.Add(new LineSegment(new Point(15, 16), true));
        figure.Segments.Add(new LineSegment(new Point(19, 20), true));
        figure.Segments.Add(new LineSegment(new Point(22, 17), true));
        figure.Segments.Add(new LineSegment(new Point(24, 22), true));
        pathGeo.Figures.Add(figure);
        pathGeo.Freeze();
        dc.DrawGeometry(null, pen, pathGeo);
    }

    private void DrawCrashReports(DrawingContext dc, Pen pen)
    {
        var pathGeo = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(17, 8), IsClosed = true };
        figure.Segments.Add(new LineSegment(new Point(26, 24), true));
        figure.Segments.Add(new LineSegment(new Point(8, 24), true));
        pathGeo.Figures.Add(figure);
        pathGeo.Freeze();
        dc.DrawGeometry(null, pen, pathGeo);

        dc.DrawLine(pen, new Point(17, 13), new Point(17, 18.5));
        dc.DrawEllipse(pen.Brush, null, new Point(17, 21.2), 0.9, 0.9);
    }

    private void DrawRecycleBin(DrawingContext dc, Pen pen)
    {
        dc.DrawLine(pen, new Point(11, 11), new Point(23, 11));
        dc.DrawLine(pen, new Point(14.5, 11), new Point(15.5, 8.5));
        dc.DrawLine(pen, new Point(15.5, 8.5), new Point(18.5, 8.5));
        dc.DrawLine(pen, new Point(18.5, 8.5), new Point(19.5, 11));

        var pathGeo = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(12, 11), IsClosed = false };
        figure.Segments.Add(new LineSegment(new Point(13.3, 25), true));
        figure.Segments.Add(new LineSegment(new Point(20.7, 25), true));
        figure.Segments.Add(new LineSegment(new Point(22, 11), true));
        pathGeo.Figures.Add(figure);
        pathGeo.Freeze();
        dc.DrawGeometry(null, pen, pathGeo);

        dc.DrawLine(pen, new Point(15.3, 14.5), new Point(15.7, 22));
        dc.DrawLine(pen, new Point(18.7, 14.5), new Point(18.3, 22));
    }

    private void DrawMemoryDumps(DrawingContext dc, Pen pen)
    {
        dc.DrawRoundedRectangle(null, pen, new Rect(9, 11, 16, 10), 1.5, 1.5);
        dc.DrawLine(pen, new Point(12, 11), new Point(12, 8.5));
        dc.DrawLine(pen, new Point(22, 11), new Point(22, 8.5));

        dc.DrawLine(pen, new Point(12, 21), new Point(12, 23.5));
        dc.DrawLine(pen, new Point(15.3, 21), new Point(15.3, 23.5));
        dc.DrawLine(pen, new Point(18.7, 21), new Point(18.7, 23.5));
        dc.DrawLine(pen, new Point(22, 21), new Point(22, 23.5));
    }

    private void DrawRecentItems(DrawingContext dc, Pen pen)
    {
        Point center = new Point(17, 17);
        dc.DrawEllipse(null, pen, center, 8.5, 8.5);
        dc.DrawLine(pen, center, new Point(17, 11.5));
        dc.DrawLine(pen, center, new Point(21, 19));
    }

    private void DrawIncompleteDownloads(DrawingContext dc, Pen pen)
    {
        dc.DrawLine(pen, new Point(17, 8), new Point(17, 12.5));
        dc.DrawLine(pen, new Point(17, 14.5), new Point(17, 17.5));

        var pathGeo = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(13, 18.5), IsClosed = false };
        figure.Segments.Add(new LineSegment(new Point(17, 22.5), true));
        figure.Segments.Add(new LineSegment(new Point(21, 18.5), true));
        pathGeo.Figures.Add(figure);
        pathGeo.Freeze();
        dc.DrawGeometry(null, pen, pathGeo);

        dc.DrawLine(pen, new Point(10, 25), new Point(24, 25));
    }

    private void DrawShaderCache(DrawingContext dc, Pen pen)
    {
        dc.DrawRoundedRectangle(null, pen, new Rect(11, 11, 12, 12), 1.5, 1.5);
        double[] coords = { 14, 17, 20 };
        foreach (var x in coords)
        {
            dc.DrawLine(pen, new Point(x, 11), new Point(x, 8.5));
            dc.DrawLine(pen, new Point(x, 23), new Point(x, 25.5));
        }
        foreach (var y in coords)
        {
            dc.DrawLine(pen, new Point(11, y), new Point(8.5, y));
            dc.DrawLine(pen, new Point(23, y), new Point(25.5, y));
        }
    }
}

