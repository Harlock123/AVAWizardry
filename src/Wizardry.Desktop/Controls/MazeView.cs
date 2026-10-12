using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Wizardry.Core.Maze;
using Wizardry.Core.Screens;

namespace Wizardry.Desktop.Controls;

/// <summary>
/// The first-person wireframe view down the corridor, in the manner of the early 80s: white lines
/// on black, each square ahead a smaller frame. Doors are drawn in amber, sealed doors in red;
/// with light the party sees further, and sees secret doors.
/// </summary>
public sealed class MazeView : Control
{
    public static readonly StyledProperty<Game?> GameProperty =
        AvaloniaProperty.Register<MazeView, Game?>(nameof(Game));

    public static readonly StyledProperty<int> VersionProperty =
        AvaloniaProperty.Register<MazeView, int>(nameof(Version));

    static MazeView() => AffectsRender<MazeView>(GameProperty, VersionProperty);

    public Game? Game { get => GetValue(GameProperty); set => SetValue(GameProperty, value); }

    /// <summary>Bumped by the view model after every key, so the view redraws.</summary>
    public int Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    // How big each successive frame is, as a fraction of the nearest.
    private static readonly double[] Frames = [1.0, 0.64, 0.42, 0.28, 0.19, 0.13, 0.09];

    public override void Render(DrawingContext dc)
    {
        var bounds = new Rect(Bounds.Size);
        dc.FillRectangle(Art.Background, bounds);
        if (Game is not { CurrentLevel: { } level } game) return;
        var w = game.World;
        double cx = bounds.Width / 2, cy = bounds.Height / 2;
        double hw = bounds.Width / 2 - 6, hh = bounds.Height / 2 - 6;
        Rect Frame(int d) => new(cx - hw * Frames[d], cy - hh * Frames[d], 2 * hw * Frames[d], 2 * hh * Frames[d]);

        dc.DrawRectangle(null, Art.Frame, Frame(0));
        if (level.FeatureAt(w.X, w.Y) == Feature.Dark)
        {
            Art.Text(dc, "DARKNESS", new Point(cx, cy), 28, Art.DimBrush, center: true);
            return;
        }

        bool lit = game.Lit;
        int depth = lit ? 6 : 4;
        var line = Art.Line;
        var faint = Art.FaintLine;
        int x = w.X, y = w.Y;
        var f = w.Facing;
        for (int i = 0; i < depth; i++)
        {
            if (!Level.InBounds(x, y)) break;
            if (i > 0 && level.FeatureAt(x, y) == Feature.Dark)
            {
                // Darkness ahead swallows the corridor.
                break;
            }
            var near = Frame(i);
            var far = Frame(i + 1);
            var pen = i >= 4 ? faint : line;

            DrawSide(dc, level, x, y, f.Left(), f, near, far, left: true, lit, pen);
            DrawSide(dc, level, x, y, f.Right(), f, near, far, left: false, lit, pen);

            if (i > 0) DrawFloorFeature(dc, level.FeatureAt(x, y), near, far);

            var front = level.EdgeAt(x, y, f);
            if (front != Edge.Open)
            {
                dc.DrawRectangle(null, pen, far);
                if (front == Edge.Door || front == Edge.Sealed || (front == Edge.Secret && lit))
                    DrawDoor(dc, far, front, faceOn: true);
                break;
            }
            if (i == depth - 1)
            {
                // The corridor runs on into the dark.
                dc.DrawLine(faint, far.TopLeft, far.TopRight);
                dc.DrawLine(faint, far.BottomLeft, far.BottomRight);
            }
            x += f.Dx();
            y += f.Dy();
        }

        // Compass and, with LOKAR, the party's position.
        Art.Text(dc, w.Facing.ToString().ToUpperInvariant(), new Point(cx, 10), 22, Art.AccentBrush, center: true);
        if (game.ShowLocation)
            Art.Text(dc, $"L{w.Depth}  {w.X}E {w.Y}N", new Point(14, bounds.Height - 30), 22, Art.InfoBrush);
        if (lit) Art.Text(dc, "*", new Point(bounds.Width - 24, 8), 26, Art.GoldBrush);
    }

    /// <summary>One side of a square: a wall (maybe with a door), or an opening onto the side passage.</summary>
    private static void DrawSide(DrawingContext dc, Level level, int x, int y, Dir side, Dir facing, Rect near, Rect far, bool left, bool lit, IPen pen)
    {
        var edge = level.EdgeAt(x, y, side);
        double nx = left ? near.Left : near.Right;
        double fx = left ? far.Left : far.Right;
        if (edge != Edge.Open)
        {
            // A wall, seen at an angle.
            dc.DrawLine(pen, new Point(nx, near.Top), new Point(fx, far.Top));
            dc.DrawLine(pen, new Point(nx, near.Bottom), new Point(fx, far.Bottom));
            dc.DrawLine(pen, new Point(fx, far.Top), new Point(fx, far.Bottom));
            if (edge == Edge.Door || edge == Edge.Sealed || (edge == Edge.Secret && lit))
                DrawSideDoor(dc, nx, fx, near, far, edge);
            return;
        }
        // An opening: the far wall of the side passage, if there is one.
        int sx = x + side.Dx(), sy = y + side.Dy();
        var beyond = level.EdgeAt(sx, sy, facing);
        dc.DrawLine(pen, new Point(nx, far.Top), new Point(fx, far.Top));
        dc.DrawLine(pen, new Point(nx, far.Bottom), new Point(fx, far.Bottom));
        dc.DrawLine(pen, new Point(fx, far.Top), new Point(fx, far.Bottom));
        if (beyond is Edge.Door or Edge.Sealed)
        {
            var r = new Rect(Math.Min(nx, fx), far.Top, Math.Abs(fx - nx), far.Height);
            DrawDoor(dc, r, beyond, faceOn: true);
        }
    }

    private static void DrawDoor(DrawingContext dc, Rect wall, Edge kind, bool faceOn)
    {
        var pen = kind == Edge.Sealed ? Art.SealedPen : kind == Edge.Secret ? Art.SecretPen : Art.DoorPen;
        double dw = wall.Width * 0.5, dh = wall.Height * 0.78;
        var door = new Rect(wall.Center.X - dw / 2, wall.Bottom - dh, dw, dh);
        dc.DrawRectangle(null, pen, door);
        if (kind == Edge.Sealed)
            dc.DrawLine(pen, door.TopLeft, door.BottomRight);
        else
            dc.DrawEllipse(null, pen, new Point(door.Right - dw * 0.15, door.Center.Y), Math.Max(1.5, dw * 0.04), Math.Max(1.5, dw * 0.04));
    }

    private static void DrawSideDoor(DrawingContext dc, double nx, double fx, Rect near, Rect far, Edge kind)
    {
        var pen = kind == Edge.Sealed ? Art.SealedPen : kind == Edge.Secret ? Art.SecretPen : Art.DoorPen;
        // A door in a receding wall: a trapezoid between 25% and 75% of the way along.
        double Lerp(double a, double b, double t) => a + (b - a) * t;
        double x1 = Lerp(nx, fx, 0.25), x2 = Lerp(nx, fx, 0.75);
        double top1 = Lerp(near.Top, far.Top, 0.25), top2 = Lerp(near.Top, far.Top, 0.75);
        double bot1 = Lerp(near.Bottom, far.Bottom, 0.25), bot2 = Lerp(near.Bottom, far.Bottom, 0.75);
        double h1 = bot1 - top1, h2 = bot2 - top2;
        var a = new Point(x1, bot1 - h1 * 0.78);
        var b = new Point(x2, bot2 - h2 * 0.78);
        dc.DrawLine(pen, new Point(x1, bot1), a);
        dc.DrawLine(pen, a, b);
        dc.DrawLine(pen, b, new Point(x2, bot2));
    }

    private static void DrawFloorFeature(DrawingContext dc, Feature feature, Rect near, Rect far)
    {
        // The floor of the square lies between the near and far frames' bottom edges.
        double top = far.Bottom, bottom = near.Bottom;
        double mid = (top + bottom) / 2;
        double cx = far.Center.X;
        double half = far.Width * 0.3;
        switch (feature)
        {
            case Feature.StairsDown:
                for (int k = 0; k < 4; k++)
                {
                    double yy = top + (bottom - top) * (0.15 + k * 0.2);
                    double hw = half * (0.6 + k * 0.15);
                    dc.DrawLine(Art.FeaturePen, new Point(cx - hw, yy), new Point(cx + hw, yy));
                }
                Art.Text(dc, "DOWN", new Point(cx, far.Bottom - far.Height * 0.25), Math.Max(10, far.Height * 0.18), Art.GoldBrush, center: true);
                break;
            case Feature.StairsUp:
                dc.DrawLine(Art.FeaturePen, new Point(cx - half * 0.4, mid + (bottom - top) * 0.3), new Point(cx - half * 0.4, far.Top + far.Height * 0.2));
                dc.DrawLine(Art.FeaturePen, new Point(cx + half * 0.4, mid + (bottom - top) * 0.3), new Point(cx + half * 0.4, far.Top + far.Height * 0.2));
                for (int k = 0; k < 5; k++)
                {
                    double yy = far.Top + far.Height * 0.2 + (mid + (bottom - top) * 0.3 - far.Top - far.Height * 0.2) * k / 4;
                    dc.DrawLine(Art.FeaturePen, new Point(cx - half * 0.4, yy), new Point(cx + half * 0.4, yy));
                }
                Art.Text(dc, "UP", new Point(cx, far.Top + far.Height * 0.05), Math.Max(10, far.Height * 0.16), Art.GoldBrush, center: true);
                break;
            case Feature.Lift:
                var cage = new Rect(cx - half * 0.6, far.Top + far.Height * 0.15, half * 1.2, mid - far.Top - far.Height * 0.15);
                dc.DrawRectangle(null, Art.FeaturePen, cage);
                for (int k = 1; k < 4; k++)
                    dc.DrawLine(Art.FeaturePen, new Point(cage.Left + cage.Width * k / 4, cage.Top), new Point(cage.Left + cage.Width * k / 4, cage.Bottom));
                break;
            case Feature.Message:
                Art.Text(dc, "~ ~ ~", new Point(cx, far.Center.Y), Math.Max(10, far.Height * 0.2), Art.InfoBrush, center: true);
                break;
        }
    }
}

/// <summary>The palette and helpers the picture controls share.</summary>
internal static class Art
{
    public static readonly IBrush Background = new SolidColorBrush(Color.Parse("#05060A"));
    public static readonly IBrush InkBrush = new SolidColorBrush(Color.Parse("#E8E6D9"));
    public static readonly IBrush DimBrush = new SolidColorBrush(Color.Parse("#6F6C64"));
    public static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#A5A1FF"));
    public static readonly IBrush GoldBrush = new SolidColorBrush(Color.Parse("#EDE65F"));
    public static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#7FC8E8"));
    public static readonly IBrush BadBrush = new SolidColorBrush(Color.Parse("#FF6B6B"));
    public static readonly IBrush GoodBrush = new SolidColorBrush(Color.Parse("#8FD16A"));
    public static readonly IBrush FloorBrush = new SolidColorBrush(Color.Parse("#12132A"));

    public static readonly IPen Line = new Pen(InkBrush, 2);
    public static readonly IPen FaintLine = new Pen(DimBrush, 1.5);
    public static readonly IPen Frame = new Pen(new SolidColorBrush(Color.Parse("#34345A")), 2);
    public static readonly IPen DoorPen = new Pen(new SolidColorBrush(Color.Parse("#E0A05C")), 2);
    public static readonly IPen SealedPen = new Pen(BadBrush, 2.5);
    public static readonly IPen SecretPen = new Pen(InfoBrush, 1.5, new DashStyle([3, 3], 0));
    public static readonly IPen FeaturePen = new Pen(GoldBrush, 2);
    public static readonly IPen AccentPen = new Pen(AccentBrush, 2);
    public static readonly IPen GoldPen = new Pen(GoldBrush, 2);
    public static readonly IPen InfoPen = new Pen(InfoBrush, 2);
    public static readonly IPen BadPen = new Pen(BadBrush, 2);
    public static readonly IPen DimPen = new Pen(DimBrush, 1.5);
    public static readonly IPen GoodPen = new Pen(GoodBrush, 2);

    public static readonly Typeface Retro = new(new FontFamily("avares://AVAWizardry/Assets/Fonts#VT323"));

    public static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        dc.DrawText(ft, center ? new Point(at.X - ft.Width / 2, at.Y) : at);
    }
}
