using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MegaDownloader.UI.Controls;

public sealed class TransferGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> HistoryProperty =
        AvaloniaProperty.Register<TransferGraph, IReadOnlyList<double>?>(nameof(History));
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<TransferGraph, IBrush?>(nameof(LineBrush));
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TransferGraph, IBrush?>(nameof(GridBrush));

    static TransferGraph() => AffectsRender<TransferGraph>(HistoryProperty, LineBrushProperty, GridBrushProperty);

    public IReadOnlyList<double>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 12 || Bounds.Height < 8)
            return;

        double height = Bounds.Height - 4;
        double width = Bounds.Width - 2;
        var gridPen = new Pen(GridBrush, 1);
        for (int line = 0; line <= 2; line++)
        {
            double y = 2 + height * line / 2;
            context.DrawLine(gridPen, new Point(1, y), new Point(1 + width, y));
        }

        if (History is not { Count: > 1 } samples || samples.Max() <= 0)
            return;

        double scale = samples.Max();
        var trace = new StreamGeometry();
        using (var stream = trace.Open())
        {
            stream.BeginFigure(new Point(1, 2 + height * (1 - samples[0] / scale)), false);
            for (int i = 1; i < samples.Count; i++)
                stream.LineTo(new Point(1 + width * i / (samples.Count - 1), 2 + height * (1 - samples[i] / scale)));
            stream.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(LineBrush, 1.7), trace);
    }
}
