using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CleanSweep.Controls;

public enum IconType
{
    Settings,
    Startup
}

public class IconButton : Button
{
    private const double ButtonSize = 34.0;

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(
            nameof(IconKind),
            typeof(IconType),
            typeof(IconButton),
            new FrameworkPropertyMetadata(IconType.Settings, FrameworkPropertyMetadataOptions.AffectsRender));

    public IconType IconKind
    {
        get => (IconType)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public IconButton()
    {
        Width = ButtonSize;
        Height = ButtonSize;
        Cursor = Cursors.Hand;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        bool isHovered = IsMouseOver;

        // Hover background
        if (isHovered)
        {
            var hoverBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B1F27"));
            hoverBrush.Freeze();
            dc.DrawRoundedRectangle(hoverBrush, null, new Rect(0, 0, ButtonSize, ButtonSize), 8, 8);
        }

        var iconColor = (Color)ColorConverter.ConvertFromString(isHovered ? "#43C6AC" : "#8B93A3");
        var pen = new Pen(new SolidColorBrush(iconColor), 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        if (IconKind == IconType.Settings)
        {
            DrawGear(dc, pen);
        }
        else
        {
            DrawRocket(dc, pen);
        }
    }

    private void DrawGear(DrawingContext dc, Pen pen)
    {
        Point center = new Point(ButtonSize / 2, ButtonSize / 2);
        double outerR = 6.5;
        double innerR = 2.6;

        for (int i = 0; i < 8; i++)
        {
            double angle = i * 45.0 * Math.PI / 180.0;
            double x1 = center.X + (outerR + 1.0) * Math.Sin(angle);
            double y1 = center.Y - (outerR + 1.0) * Math.Cos(angle);
            double x2 = center.X + (outerR + 3.5) * Math.Sin(angle);
            double y2 = center.Y - (outerR + 3.5) * Math.Cos(angle);
            dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        }

        dc.DrawEllipse(null, pen, center, outerR, outerR);
        dc.DrawEllipse(null, pen, center, innerR, innerR);
    }

    private void DrawRocket(DrawingContext dc, Pen pen)
    {
        var pathGeo = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(17, 8), IsClosed = true };
        figure.Segments.Add(new LineSegment(new Point(21, 17), true));
        figure.Segments.Add(new LineSegment(new Point(21, 23), true));
        figure.Segments.Add(new LineSegment(new Point(13, 23), true));
        figure.Segments.Add(new LineSegment(new Point(13, 17), true));
        pathGeo.Figures.Add(figure);
        pathGeo.Freeze();

        dc.DrawGeometry(null, pen, pathGeo);
        dc.DrawEllipse(null, pen, new Point(17, 15.5), 1.8, 1.8);
        dc.DrawLine(pen, new Point(13, 20.5), new Point(9.5, 25.5));
        dc.DrawLine(pen, new Point(21, 20.5), new Point(24.5, 25.5));
    }
}

