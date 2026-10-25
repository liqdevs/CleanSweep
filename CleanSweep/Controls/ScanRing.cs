using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CleanSweep.Controls;

public class ScanRing : FrameworkElement
{
    private const double RingWidth = 14.0;
    private static readonly SolidColorBrush TrackBrush = CreateFrozenBrush("#232833");
    private static readonly SolidColorBrush TitleBrush = CreateFrozenBrush("#F2F4F8");
    private static readonly SolidColorBrush SubBrush = CreateFrozenBrush("#5C6376");
    private static readonly Typeface TitleTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface SubTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Pen TrackPen = CreatePen(TrackBrush);

    private Pen _progressPen = CreatePen(CreateFrozenBrush("#43C6AC"));
    private string? _cachedTitleText;
    private string? _cachedSubText;
    private double _cachedTextWidth = double.NaN;
    private double _cachedPixelsPerDip = double.NaN;
    private FormattedText? _cachedTitle;
    private FormattedText? _cachedSub;

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
            new FrameworkPropertyMetadata(CreateFrozenBrush("#43C6AC"), FrameworkPropertyMetadataOptions.AffectsRender, OnRingBrushChanged));

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

    private static void OnRingBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        ((ScanRing)sender)._progressPen = CreatePen((Brush)e.NewValue);
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
        dc.DrawEllipse(null, TrackPen, new Point(cx, cy), radius, radius);

        // 2. Active progress arc
        double progress = Progress;
        if (progress > 0.001)
        {
            if (progress >= 0.999)
            {
                dc.DrawEllipse(null, _progressPen, new Point(cx, cy), radius, radius);
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

                dc.DrawGeometry(null, _progressPen, pathGeometry);
            }
        }

        // 3. Center and Sub texts
        double maxTextWidth = side * 0.72;
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        EnsureTextCache(maxTextWidth, pixelsPerDip);

        if (_cachedTitle is not null)
        {
            double titleY = string.IsNullOrEmpty(SubText) ? cy - _cachedTitle.Height / 2 : cy - _cachedTitle.Height + 2;
            dc.DrawText(_cachedTitle, new Point(cx - _cachedTitle.Width / 2, titleY));
        }

        if (_cachedSub is not null)
        {
            dc.DrawText(_cachedSub, new Point(cx - _cachedSub.Width / 2, cy + 8));
        }
    }

    private void EnsureTextCache(double maxTextWidth, double pixelsPerDip)
    {
        if (_cachedTitleText == CenterText &&
            _cachedSubText == SubText &&
            _cachedTextWidth.Equals(maxTextWidth) &&
            _cachedPixelsPerDip.Equals(pixelsPerDip))
        {
            return;
        }

        _cachedTitleText = CenterText;
        _cachedSubText = SubText;
        _cachedTextWidth = maxTextWidth;
        _cachedPixelsPerDip = pixelsPerDip;
        _cachedTitle = CreateFittedText(CenterText, TitleTypeface, TitleBrush, 18.0, 11.0, 1.0, maxTextWidth, pixelsPerDip);
        _cachedSub = CreateFittedText(SubText, SubTypeface, SubBrush, 11.0, 8.0, 0.5, maxTextWidth, pixelsPerDip);
    }

    private static FormattedText? CreateFittedText(
        string text,
        Typeface typeface,
        Brush brush,
        double fontSize,
        double minimumFontSize,
        double step,
        double maxTextWidth,
        double pixelsPerDip)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        FormattedText formattedText;
        do
        {
            formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                pixelsPerDip);
            if (formattedText.Width <= maxTextWidth || fontSize <= minimumFontSize)
            {
                return formattedText;
            }
            fontSize -= step;
        } while (fontSize >= minimumFontSize);

        return formattedText;
    }

    private static SolidColorBrush CreateFrozenBrush(string color) =>
        CreateFrozenBrush((Color)ColorConverter.ConvertFromString(color));

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush)
    {
        Brush frozenBrush = brush.IsFrozen ? brush : brush.CloneCurrentValue();
        if (frozenBrush.CanFreeze)
        {
            frozenBrush.Freeze();
        }

        var pen = new Pen(frozenBrush, RingWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        return pen;
    }
}
