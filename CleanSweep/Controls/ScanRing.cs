using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CleanSweep.Controls;

public class ScanRing : FrameworkElement
{
    private const double RingWidth = 14.0;

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(ScanRing),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterTextProperty =
        DependencyProperty.Register(
            nameof(CenterText),
            typeof(string),
            typeof(ScanRing),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SubTextProperty =
        DependencyProperty.Register(
            nameof(SubText),
            typeof(string),
            typeof(ScanRing),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(
            nameof(RingBrush),
            typeof(Brush),
            typeof(ScanRing),
            new FrameworkPropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#43C6AC")), FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, Math.Clamp(value, 0.0, 1.0));
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string SubText
    {
        get => (string)GetValue(SubTextProperty);
        set => SetValue(SubTextProperty, value);
    }

    public Brush RingBrush
    {
        get => (Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public ScanRing()
    {
        Width = 190;
        Height = 190;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;
        double side = Math.Min(width, height) - RingWidth * 2;
        double cx = width / 2;
        double cy = height / 2;
        double radius = side / 2;

        if (radius <= 0) return;

        // 1. Background track ring
        var trackPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232833")), RingWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        trackPen.Freeze();
        dc.DrawEllipse(null, trackPen, new Point(cx, cy), radius, radius);

        // 2. Active progress arc
        double progress = Progress;
        if (progress > 0.001)
        {
            var ringPen = new Pen(RingBrush, RingWidth)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            ringPen.Freeze();

            if (progress >= 0.999)
            {
                dc.DrawEllipse(null, ringPen, new Point(cx, cy), radius, radius);
            }
            else
            {
                double angleDeg = progress * 360.0;
                double angleRad = (angleDeg - 90.0) * Math.PI / 180.0;

                Point startPoint = new Point(cx, cy - radius);
                Point endPoint = new Point(cx + radius * Math.Cos(angleRad), cy + radius * Math.Sin(angleRad));

                var pathFigure = new PathFigure
                {
                    StartPoint = startPoint,
                    IsClosed = false
                };
                pathFigure.Segments.Add(new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    IsLargeArc = angleDeg > 180.0,
                    SweepDirection = SweepDirection.Clockwise
                });

                var pathGeometry = new PathGeometry();
                pathGeometry.Figures.Add(pathFigure);
                pathGeometry.Freeze();

                dc.DrawGeometry(null, ringPen, pathGeometry);
            }
        }

        // 3. Center and Sub texts
        var typefaceTitle = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var typefaceSub = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var textBrushTitle = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F2F4F8"));
        var textBrushSub = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C6376"));
        textBrushTitle.Freeze();
        textBrushSub.Freeze();

        double maxTextWidth = side * 0.72;

        if (!string.IsNullOrEmpty(CenterText))
        {
            double fontSize = 18.0;
            FormattedText ftTitle;
            do
            {
                ftTitle = new FormattedText(
                    CenterText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typefaceTitle,
                    fontSize,
                    textBrushTitle,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                if (ftTitle.Width <= maxTextWidth || fontSize <= 11) break;
                fontSize -= 1.0;
            } while (fontSize > 10);

            double titleY = string.IsNullOrEmpty(SubText) ? cy - ftTitle.Height / 2 : cy - ftTitle.Height + 2;
            dc.DrawText(ftTitle, new Point(cx - ftTitle.Width / 2, titleY));
        }

        if (!string.IsNullOrEmpty(SubText))
        {
            double fontSize = 11.0;
            FormattedText ftSub;
            do
            {
                ftSub = new FormattedText(
                    SubText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typefaceSub,
                    fontSize,
                    textBrushSub,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                if (ftSub.Width <= maxTextWidth || fontSize <= 8) break;
                fontSize -= 0.5;
            } while (fontSize > 8);

            dc.DrawText(ftSub, new Point(cx - ftSub.Width / 2, cy + 8));
        }
    }
}

