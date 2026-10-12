using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Wizardry.Core.Maze;
using Wizardry.Core.Screens;

namespace Wizardry.Desktop.Controls;

/// <summary>
/// A convenience the original didn't have (players drew their own on graph paper): the squares
/// of this level the party has seen, with walls, doors and stairs, and an arrow for the party.
/// </summary>
public sealed class AutomapView : Control
{
    public static readonly StyledProperty<Game?> GameProperty =
        AvaloniaProperty.Register<AutomapView, Game?>(nameof(Game));

    public static readonly StyledProperty<int> VersionProperty =
        AvaloniaProperty.Register<AutomapView, int>(nameof(Version));

    static AutomapView() => AffectsRender<AutomapView>(GameProperty, VersionProperty);

    public Game? Game { get => GetValue(GameProperty); set => SetValue(GameProperty, value); }
    public int Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    public override void Render(DrawingContext dc)
    {
        var bounds = new Rect(Bounds.Size);
        dc.FillRectangle(Art.Background, bounds);
        if (Game is not { CurrentLevel: { } level } game) return;
        var w = game.World;
        const int n = Level.Size;
        double cell = Math.Floor(Math.Min(bounds.Width, bounds.Height) / n);
        if (cell < 3) return;
        double ox = Math.Floor((bounds.Width - cell * n) / 2), oy = Math.Floor((bounds.Height - cell * n) / 2);
        // North is up: y grows upwards on the map.
        Rect Square(int x, int y) => new(ox + x * cell, oy + (n - 1 - y) * cell, cell, cell);
        var grid = new Pen(new SolidColorBrush(Color.Parse("#141528")), 1);
        dc.DrawRectangle(null, grid, new Rect(ox, oy, cell * n, cell * n));
        double t = Math.Max(1.5, cell * 0.12);
        var wall = new Pen(Art.InkBrush, t);
        var door = new Pen(new SolidColorBrush(Color.Parse("#E0A05C")), t);
        var sealedPen = new Pen(Art.BadBrush, t);

        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                if (!w.IsExplored(w.Depth, x, y)) continue;
                var r = Square(x, y);
                var feature = level.FeatureAt(x, y);
                dc.FillRectangle(feature == Feature.Dark ? Art.Background : Art.FloorBrush, r);
                foreach (var d in Enum.GetValues<Dir>())
                {
                    var e = level.EdgeAt(x, y, d);
                    if (e == Edge.Open) continue;
                    var (a, b) = d switch
                    {
                        Dir.North => (r.TopLeft, r.TopRight),
                        Dir.South => (r.BottomLeft, r.BottomRight),
                        Dir.East => (r.TopRight, r.BottomRight),
                        _ => (r.TopLeft, r.BottomLeft),
                    };
                    dc.DrawLine(e == Edge.Door ? door : e == Edge.Sealed ? sealedPen : wall, a, b);
                }
                string? mark = feature switch
                {
                    Feature.StairsUp => "<",
                    Feature.StairsDown => ">",
                    Feature.Lift => "L",
                    Feature.Message => "?",
                    Feature.Lair => "!",
                    _ => null,
                };
                if (mark is not null && cell >= 9)
                    Art.Text(dc, mark, new Point(r.Center.X, r.Top - cell * 0.05), cell * 0.95, Art.GoldBrush, center: true);
            }

        // The party: an arrow pointing the way it faces.
        var p = Square(w.X, w.Y);
        var c = p.Center;
        double s = cell * 0.38;
        var (fx, fy) = w.Facing switch { Dir.North => (0.0, -1.0), Dir.South => (0.0, 1.0), Dir.East => (1.0, 0.0), _ => (-1.0, 0.0) };
        var tip = new Point(c.X + fx * s, c.Y + fy * s);
        var l = new Point(c.X - fx * s * 0.6 + fy * s * 0.7, c.Y - fy * s * 0.6 - fx * s * 0.7);
        var rr = new Point(c.X - fx * s * 0.6 - fy * s * 0.7, c.Y - fy * s * 0.6 + fx * s * 0.7);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(tip, true);
            g.LineTo(l);
            g.LineTo(rr);
            g.EndFigure(true);
        }
        dc.DrawGeometry(Art.AccentBrush, null, geo);
    }
}
