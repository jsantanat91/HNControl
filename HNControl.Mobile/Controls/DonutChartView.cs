using Microsoft.Maui.Graphics;

namespace HNControl.Mobile.Controls;

public class DonutChartView : GraphicsView, IDrawable
{
    public static readonly BindableProperty NetValueProperty = BindableProperty.Create(
        nameof(NetValue), typeof(double), typeof(DonutChartView), 0d, propertyChanged: OnDataChanged);

    public static readonly BindableProperty DeductionValueProperty = BindableProperty.Create(
        nameof(DeductionValue), typeof(double), typeof(DonutChartView), 0d, propertyChanged: OnDataChanged);

    public static readonly BindableProperty BonusValueProperty = BindableProperty.Create(
        nameof(BonusValue), typeof(double), typeof(DonutChartView), 0d, propertyChanged: OnDataChanged);

    public double NetValue
    {
        get => (double)GetValue(NetValueProperty);
        set => SetValue(NetValueProperty, value);
    }

    public double DeductionValue
    {
        get => (double)GetValue(DeductionValueProperty);
        set => SetValue(DeductionValueProperty, value);
    }

    public double BonusValue
    {
        get => (double)GetValue(BonusValueProperty);
        set => SetValue(BonusValueProperty, value);
    }

    public DonutChartView()
    {
        Drawable = this;
        HeightRequest = 180;
        WidthRequest = 180;
    }

    private static void OnDataChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DonutChartView donut)
        {
            donut.Invalidate();
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var total = Math.Max(0.0001, NetValue + DeductionValue + BonusValue);
        var radius = Math.Min(dirtyRect.Width, dirtyRect.Height) * 0.38f;
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var stroke = Math.Max(10f, radius * 0.35f);

        var rect = new RectF(centerX - radius, centerY - radius, radius * 2, radius * 2);

        canvas.StrokeSize = stroke;
        canvas.StrokeLineCap = LineCap.Round;

        float start = -90f;
        DrawArc(canvas, rect, start, (float)(360 * (NetValue / total)), Color.FromArgb("#2563EB"));
        start += (float)(360 * (NetValue / total));
        DrawArc(canvas, rect, start, (float)(360 * (DeductionValue / total)), Color.FromArgb("#F59E0B"));
        start += (float)(360 * (DeductionValue / total));
        DrawArc(canvas, rect, start, (float)(360 * (BonusValue / total)), Color.FromArgb("#10B981"));
    }

    private static void DrawArc(ICanvas canvas, RectF rect, float start, float sweep, Color color)
    {
        if (sweep <= 0.01f) return;
        canvas.StrokeColor = color;
        // MAUI can render a 360deg arc as a short segment on some Android devices.
        // Draw a full ellipse stroke instead when the segment is effectively the whole ring.
        if (sweep >= 359.5f)
        {
            canvas.DrawEllipse(rect);
            return;
        }
        canvas.DrawArc(rect, start, sweep, false, false);
    }
}
