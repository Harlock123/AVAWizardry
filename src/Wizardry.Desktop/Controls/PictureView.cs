using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Screens;

namespace Wizardry.Desktop.Controls;

/// <summary>
/// Line drawings for everything that isn't the maze: the castle's buildings, the monsters a fight
/// is against, a chest, the amulet. All drawn on a 100×100 grid scaled to fit, so they stay crisp
/// at any size.
/// </summary>
public sealed class PictureView : Control
{
    public static readonly StyledProperty<Game?> GameProperty =
        AvaloniaProperty.Register<PictureView, Game?>(nameof(Game));

    public static readonly StyledProperty<int> VersionProperty =
        AvaloniaProperty.Register<PictureView, int>(nameof(Version));

    public static readonly StyledProperty<Pane> PaneProperty =
        AvaloniaProperty.Register<PictureView, Pane>(nameof(Pane));

    public static readonly StyledProperty<string> PlaceProperty =
        AvaloniaProperty.Register<PictureView, string>(nameof(Place), "castle");

    static PictureView() => AffectsRender<PictureView>(GameProperty, VersionProperty, PaneProperty, PlaceProperty);

    public Game? Game { get => GetValue(GameProperty); set => SetValue(GameProperty, value); }
    public int Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }
    public Pane Pane { get => GetValue(PaneProperty); set => SetValue(PaneProperty, value); }
    public string Place { get => GetValue(PlaceProperty); set => SetValue(PlaceProperty, value); }

    public override void Render(DrawingContext dc)
    {
        var bounds = new Rect(Bounds.Size);
        dc.FillRectangle(Art.Background, bounds);
        double size = Math.Min(bounds.Width, bounds.Height) * 0.86;
        var box = new Rect((bounds.Width - size) / 2, (bounds.Height - size) / 2, size, size);
        var c = new Canvas100(dc, box);
        switch (Pane)
        {
            case Pane.Combat when Game?.Battle is { Groups.Count: > 0 } battle:
                Monster(c, battle.Groups[0]);
                break;
            case Pane.Combat:
                Victory(c);
                break;
            case Pane.Chest:
                Chest(c);
                break;
            case Pane.Victory:
                Amulet(c);
                break;
            case Pane.Title:
                Corridor(dc, bounds);
                break;
            default:
                Place100(c, Place);
                break;
        }
    }

    // ----- the castle --------------------------------------------------------------------------

    private static void Place100(Canvas100 c, string place)
    {
        switch (place)
        {
            case "tavern": Tavern(c); break;
            case "inn": Inn(c); break;
            case "shop": Shop(c); break;
            case "temple": Temple(c); break;
            case "edge": Edge(c); break;
            case "training": Training(c); break;
            default: Castle(c); break;
        }
    }

    private static void Castle(Canvas100 c)
    {
        var ink = Art.Line;
        // Curtain wall with crenellations.
        c.Poly(ink, (10, 90), (10, 55), (90, 55), (90, 90));
        Crenels(c, ink, 10, 90, 55);
        // Towers.
        foreach (var (l, r, top) in new[] { (4.0, 20.0, 35.0), (80.0, 96.0, 35.0), (40.0, 60.0, 20.0) })
        {
            c.Poly(ink, (l, 90), (l, top), (r, top), (r, 90));
            Crenels(c, ink, l, r, top);
            c.Rect(Art.GoldPen, (l + r) / 2 - 2, top + 8, 4, 7);
        }
        // Flags.
        foreach (var x in new[] { 12.0, 50.0, 88.0 })
        {
            double top = x == 50 ? 20 : 35;
            c.Line(ink, (x, top), (x, top - 12));
            c.Poly(Art.AccentPen, (x, top - 12), (x + 8, top - 9), (x, top - 6));
        }
        // The gate.
        c.Arch(Art.GoldPen, 42, 90, 16, 20);
        for (int i = 1; i < 4; i++) c.Line(Art.DimPen, (42 + i * 4, 75), (42 + i * 4, 90));
        c.Line(ink, (0, 90), (100, 90));
    }

    private static void Crenels(Canvas100 c, IPen pen, double left, double right, double top)
    {
        for (double x = left; x < right - 1; x += 4)
            c.Poly(pen, (x, top), (x, top - 3), (x + 2, top - 3), (x + 2, top));
    }

    private static void Building(Canvas100 c, double roofPeak = 22)
    {
        c.Poly(Art.Line, (12, 92), (12, 45), (88, 45), (88, 92));
        c.Poly(Art.Line, (6, 47), (50, roofPeak), (94, 47));
        c.Line(Art.Line, (0, 92), (100, 92));
    }

    private static void Tavern(Canvas100 c)
    {
        Building(c);
        c.Arch(Art.DoorPen, 42, 92, 16, 22);
        c.Rect(Art.InfoPen, 18, 55, 14, 12);
        c.Rect(Art.InfoPen, 68, 55, 14, 12);
        // Sign on a bracket, with a tankard.
        c.Line(Art.Line, (88, 52), (98, 52));
        c.Line(Art.Line, (96, 52), (96, 56));
        c.Rect(Art.GoldPen, 90, 56, 10, 12);
        c.Rect(Art.GoldPen, 92.5, 58.5, 4, 6.5);
        c.Poly(Art.GoldPen, (96.5, 59.5), (98, 59.5), (98, 63.5), (96.5, 63.5));
        c.Text("THE GILDED FLAGON", 50, 30, 5, Art.GoldBrush);
    }

    private static void Inn(Canvas100 c)
    {
        Building(c, 18);
        c.Arch(Art.DoorPen, 44, 92, 12, 20);
        for (int i = 0; i < 3; i++)
        {
            c.Rect(Art.InfoPen, 16 + i * 26, 52, 10, 10);
            c.Rect(Art.InfoPen, 16 + i * 26, 70, 10, 10);
        }
        c.Rect(Art.GoldPen, 38, 34, 24, 8);
        c.Text("INN", 50, 34.5, 6, Art.GoldBrush);
        // The moon.
        c.Circle(Art.GoldPen, 85, 12, 6);
        c.Circle(new Pen(Art.Background, 3), 88, 10, 5);
    }

    private static void Shop(Canvas100 c)
    {
        // A counter with wares behind it.
        c.Poly(Art.Line, (5, 92), (5, 66), (95, 66), (95, 92));
        c.Line(Art.Line, (0, 70), (100, 70));
        // A sword on the wall.
        c.Line(Art.Line, (20, 15), (20, 55));
        c.Line(Art.GoldPen, (13, 48), (27, 48));
        c.Line(Art.GoldPen, (20, 48), (20, 58));
        // A shield.
        c.Poly(Art.AccentPen, (40, 20), (60, 20), (60, 38), (50, 50), (40, 38), (40, 20));
        c.Line(Art.AccentPen, (50, 20), (50, 50));
        // Scales.
        c.Line(Art.Line, (80, 30), (80, 62));
        c.Line(Art.Line, (68, 34), (92, 34));
        c.Poly(Art.GoldPen, (65, 34), (68, 44), (71, 34));
        c.Line(Art.GoldPen, (64, 44), (72, 44));
        c.Poly(Art.GoldPen, (89, 34), (92, 44), (95, 34));
        c.Line(Art.GoldPen, (88, 44), (96, 44));
        c.Line(Art.Line, (74, 62), (86, 62));
        // Coins on the counter.
        for (int i = 0; i < 4; i++) c.Circle(Art.GoldPen, 30 + i * 4, 63 - i * 1.2, 2);
    }

    private static void Temple(Canvas100 c)
    {
        c.Poly(Art.Line, (8, 30), (50, 8), (92, 30), (8, 30));
        c.Line(Art.Line, (8, 34), (92, 34));
        for (int i = 0; i < 5; i++)
        {
            double x = 14 + i * 18;
            c.Line(Art.Line, (x, 34), (x, 86));
            c.Line(Art.Line, (x + 6, 34), (x + 6, 86));
        }
        c.Line(Art.Line, (4, 86), (96, 86));
        c.Line(Art.Line, (0, 92), (100, 92));
        // A sun disc in the pediment, and candles.
        c.Circle(Art.GoldPen, 50, 22, 5);
        foreach (var x in new[] { 29.0, 65.0 })
        {
            c.Rect(Art.Line, x - 1.5, 70, 3, 14);
            c.Poly(Art.GoldPen, (x, 63), (x + 2, 68), (x, 70), (x - 2, 68), (x, 63));
        }
    }

    private static void Edge(Canvas100 c)
    {
        // The end of the town wall.
        c.Poly(Art.Line, (0, 40), (30, 40), (30, 90));
        Crenels(c, Art.Line, 0, 30, 40);
        // A stair going down into the ground, in perspective.
        c.Line(Art.Line, (0, 90), (100, 90));
        c.Poly(Art.GoldPen, (48, 90), (52, 70), (92, 70), (96, 90));
        for (int i = 1; i < 5; i++)
        {
            double y = 70 + i * 4, inset = 4 - i * 0.8;
            c.Line(Art.GoldPen, (48 + inset, y), (96 - inset, y));
        }
        // The yard's practice post, and a sign.
        c.Line(Art.Line, (72, 70), (72, 40));
        c.Rect(Art.InfoPen, 58, 34, 30, 9);
        c.Text("PROVING GROUNDS", 73, 35.5, 4.3, Art.InfoBrush);
    }

    private static void Training(Canvas100 c)
    {
        // A straw dummy and a target.
        c.Line(Art.Line, (30, 90), (30, 30));
        c.Line(Art.Line, (14, 45), (46, 45));
        c.Circle(Art.Line, 30, 24, 7);
        c.Poly(Art.GoldPen, (22, 45), (24, 70), (36, 70), (38, 45));
        foreach (var r in new[] { 16.0, 11.0, 6.0, 2.0 })
            c.Circle(r == 2 ? Art.BadPen : Art.GoldPen, 72, 50, r);
        c.Line(Art.Line, (72, 66), (72, 90));
        c.Line(Art.Line, (0, 90), (100, 90));
        // An arrow in the target.
        c.Line(Art.AccentPen, (74, 49), (92, 40));
        c.Poly(Art.AccentPen, (90, 37), (93, 40), (90, 43));
    }

    // ----- monsters ---------------------------------------------------------------------------

    private static void Monster(Canvas100 c, FoeGroup group)
    {
        var def = group.Def;
        var pen = group.Identified ? Art.Line : Art.DimPen;
        var accent = group.Identified ? Art.AccentPen : Art.DimPen;
        var eye = group.Identified ? Art.BadPen : Art.DimPen;
        string shape = Shape(def);
        switch (shape)
        {
            case "slime": Slime(c, pen, eye); break;
            case "cloud": Cloud(c, pen); break;
            case "bat": Bat(c, pen, eye); break;
            case "insect": Insect(c, pen, eye); break;
            case "beast": Beast(c, pen, eye, wings: def.Family.HasFlag(Family.Myth)); break;
            case "reptile": Reptile(c, pen, eye); break;
            case "undead": Skeleton(c, pen, eye); break;
            case "dragon": Dragon(c, pen, accent, eye); break;
            case "giant": Figure(c, pen, accent, eye, scale: 1.0, weapon: "club"); break;
            case "demon": Demon(c, pen, accent, eye); break;
            case "statue": Statue(c, pen, eye); break;
            case "robed": Robed(c, pen, accent, eye, crown: def.Unique, snakes: def.Name == "Medusa"); break;
            default: Figure(c, pen, accent, eye, scale: def.Name.StartsWith("Kobold") ? 0.7 : 0.85, weapon: "sword"); break;
        }
        c.Text(group.Label, 50, 94, 5.5, group.Identified ? Art.GoldBrush : Art.DimBrush);
    }

    private static string Shape(MonsterDef def)
    {
        if (def.UnknownName is "Slime") return "slime";
        if (def.UnknownName is "Cloud") return "cloud";
        if (def.UnknownName is "Flying Thing") return "bat";
        if (def.Family.HasFlag(Family.Insect)) return "insect";
        if (def.Family.HasFlag(Family.Dragon)) return "dragon";
        if (def.Family.HasFlag(Family.Demon)) return "demon";
        if (def.UnknownName is "Reptile") return "reptile";
        if (def.UnknownName is "Statue" or "Huge Statue") return "statue";
        if (def.UnknownName is "Beast") return "beast";
        if (def.Family.HasFlag(Family.Undead) && !def.Family.HasFlag(Family.Animal) && def.UnknownName == "Undead") return "undead";
        if (def.Family.HasFlag(Family.Animal) || def.Family.HasFlag(Family.Were)) return "beast";
        if (def.Family.HasFlag(Family.Giant)) return "giant";
        if (def.UnknownName.Contains("Robes") || def.UnknownName.Contains("Rags") || def.UnknownName.StartsWith("Wom")) return "robed";
        return "humanoid";
    }

    private static void Eyes(Canvas100 c, IPen eye, double x, double y, double gap, double r = 1.2)
    {
        c.Circle(eye, x - gap, y, r);
        c.Circle(eye, x + gap, y, r);
    }

    private static void Figure(Canvas100 c, IPen pen, IPen accent, IPen eye, double scale, string weapon)
    {
        // A standing figure, scaled about the ground line.
        (double, double) P(double x, double y) => (50 + (x - 50) * scale, 90 - (90 - y) * scale);
        var head = P(50, 18);
        c.Circle(pen, head.Item1, head.Item2, 7 * scale);
        Eyes(c, eye, head.Item1, head.Item2, 2.5 * scale, 1 * scale);
        c.Poly(pen, P(40, 30), P(60, 30), P(58, 58), P(42, 58), P(40, 30));
        c.Poly(pen, P(44, 58), P(40, 88));
        c.Poly(pen, P(56, 58), P(60, 88));
        c.Poly(pen, P(40, 32), P(28, 50), P(30, 56));
        c.Poly(pen, P(60, 32), P(72, 48));
        if (weapon == "sword")
        {
            c.Line(accent, P(72, 48), P(80, 14));
            c.Line(accent, P(67, 44), P(77, 47));
            // A shield.
            c.Poly(accent, P(22, 44), P(34, 44), P(34, 58), P(28, 64), P(22, 58), P(22, 44));
        }
        else
        {
            c.Poly(accent, P(72, 48), P(84, 20), P(90, 22), P(76, 50));
        }
    }

    private static void Robed(Canvas100 c, IPen pen, IPen accent, IPen eye, bool crown, bool snakes)
    {
        c.Circle(pen, 50, 22, 8);
        Eyes(c, eye, 50, 22, 3);
        c.Poly(pen, (40, 18), (50, 6), (60, 18));
        c.Poly(pen, (42, 30), (58, 30), (70, 88), (30, 88), (42, 30));
        c.Line(pen, (44, 40), (56, 40));
        c.Poly(pen, (44, 34), (32, 52), (36, 56));
        // A staff with a glowing head.
        c.Line(accent, (66, 88), (66, 14));
        c.Circle(Art.GoldPen, 66, 11, 3.5);
        c.Poly(pen, (56, 34), (66, 44));
        if (crown)
        {
            c.Poly(Art.GoldPen, (42, 10), (44, 3), (47, 8), (50, 1), (53, 8), (56, 3), (58, 10));
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                c.Line(Art.GoldPen, (66 + Math.Cos(a) * 5, 11 + Math.Sin(a) * 5), (66 + Math.Cos(a) * 9, 11 + Math.Sin(a) * 9));
            }
        }
        if (snakes)
            for (int i = 0; i < 6; i++)
            {
                double x = 42 + i * 3.2;
                c.Poly(Art.GoodPen, (x, 16), (x - 3, 10), (x + 1, 6), (x - 1, 1));
            }
    }

    private static void Skeleton(Canvas100 c, IPen pen, IPen eye)
    {
        c.Circle(pen, 50, 18, 8);
        c.Circle(eye, 47, 17, 2);
        c.Circle(eye, 53, 17, 2);
        c.Line(pen, (46, 23), (54, 23));
        c.Line(pen, (50, 26), (50, 60));
        for (int i = 0; i < 4; i++)
        {
            double y = 32 + i * 6, w = 12 - i * 1.5;
            c.Poly(pen, (50 - w, y + 3), (50, y), (50 + w, y + 3));
        }
        c.Poly(pen, (42, 60), (58, 60), (54, 64), (46, 64), (42, 60));
        c.Poly(pen, (46, 64), (42, 88));
        c.Poly(pen, (54, 64), (58, 88));
        c.Poly(pen, (40, 30), (30, 48), (34, 60));
        c.Poly(pen, (60, 30), (72, 44), (76, 30));
    }

    private static void Slime(Canvas100 c, IPen pen, IPen eye)
    {
        c.Poly(pen, (14, 88), (18, 70), (28, 58), (40, 50), (52, 48), (66, 54), (78, 64), (86, 76), (88, 88), (14, 88));
        c.Poly(pen, (30, 88), (26, 94));
        c.Poly(pen, (70, 88), (74, 95));
        Eyes(c, eye, 50, 64, 8, 3);
        c.Circle(Art.GoodPen, 32, 76, 2.5);
        c.Circle(Art.GoodPen, 70, 78, 1.8);
    }

    private static void Cloud(Canvas100 c, IPen pen)
    {
        foreach (var (x, y, r) in new[] { (35.0, 50.0, 14.0), (52.0, 42.0, 16.0), (68.0, 52.0, 13.0), (45.0, 60.0, 12.0), (60.0, 62.0, 11.0) })
            c.Circle(pen, x, y, r);
        for (int i = 0; i < 5; i++) c.Line(Art.GoodPen, (36 + i * 7, 74), (34 + i * 7, 86));
    }

    private static void Bat(Canvas100 c, IPen pen, IPen eye)
    {
        c.Circle(pen, 50, 46, 6);
        Eyes(c, eye, 50, 45, 2.2, 1);
        c.Poly(pen, (46, 42), (44, 34), (48, 40));
        c.Poly(pen, (54, 42), (56, 34), (52, 40));
        c.Poly(pen, (45, 48), (30, 32), (12, 40), (20, 46), (24, 54), (34, 50), (45, 52));
        c.Poly(pen, (55, 48), (70, 32), (88, 40), (80, 46), (76, 54), (66, 50), (55, 52));
    }

    private static void Insect(Canvas100 c, IPen pen, IPen eye)
    {
        c.Circle(pen, 50, 58, 13);
        c.Circle(pen, 50, 38, 8);
        Eyes(c, eye, 50, 36, 3, 1.5);
        for (int i = 0; i < 4; i++)
        {
            double y = 50 + i * 6;
            c.Poly(pen, (38, y), (24, y - 10 + i * 2), (12, y + 8 + i * 4));
            c.Poly(pen, (62, y), (76, y - 10 + i * 2), (88, y + 8 + i * 4));
        }
        c.Poly(pen, (46, 31), (40, 20));
        c.Poly(pen, (54, 31), (60, 20));
    }

    private static void Beast(Canvas100 c, IPen pen, IPen eye, bool wings)
    {
        // A four-legged animal, side on.
        c.Poly(pen, (24, 52), (34, 44), (64, 44), (76, 50), (74, 64), (28, 64), (24, 52));
        c.Poly(pen, (76, 50), (84, 38), (94, 42), (92, 50), (80, 56));
        c.Circle(eye, 88, 43, 1.3);
        c.Poly(pen, (86, 38), (88, 32), (90, 38));
        foreach (var x in new[] { 30.0, 40.0, 62.0, 70.0 })
            c.Poly(pen, (x, 64), (x - 1, 84), (x + 3, 84));
        c.Poly(pen, (24, 52), (12, 46), (6, 52));
        if (wings)
            c.Poly(Art.AccentPen, (44, 44), (36, 20), (52, 28), (60, 16), (62, 44));
    }

    private static void Reptile(Canvas100 c, IPen pen, IPen eye)
    {
        c.Poly(pen, (10, 70), (28, 60), (60, 58), (82, 62), (94, 58), (90, 68), (64, 72), (24, 74), (10, 70));
        c.Circle(eye, 88, 61, 1.5);
        foreach (var x in new[] { 30.0, 44.0, 58.0, 70.0 })
            c.Poly(pen, (x, 72), (x - 4, 84), (x + 2, 84));
        for (int i = 0; i < 6; i++) c.Poly(Art.GoodPen, (30 + i * 7, 60), (33 + i * 7, 54), (36 + i * 7, 59));
    }

    private static void Dragon(Canvas100 c, IPen pen, IPen accent, IPen eye)
    {
        // Body and long neck.
        c.Poly(pen, (20, 78), (30, 62), (54, 58), (66, 66), (60, 80), (20, 78));
        c.Poly(pen, (54, 58), (62, 40), (72, 26), (84, 22), (92, 26), (86, 32), (76, 34), (70, 46), (66, 66));
        c.Circle(eye, 85, 25, 1.4);
        c.Poly(Art.BadPen, (92, 26), (98, 22), (96, 30));
        // Wings.
        c.Poly(accent, (36, 60), (22, 26), (40, 34), (46, 18), (54, 58));
        // Legs and tail.
        c.Poly(pen, (30, 78), (28, 90));
        c.Poly(pen, (54, 80), (56, 90));
        c.Poly(pen, (20, 78), (8, 84), (4, 76), (10, 80));
    }

    private static void Demon(Canvas100 c, IPen pen, IPen accent, IPen eye)
    {
        Figure(c, pen, accent, eye, scale: 0.95, weapon: "club");
        c.Poly(pen, (44, 14), (40, 4), (46, 11));
        c.Poly(pen, (56, 14), (60, 4), (54, 11));
        c.Poly(accent, (40, 34), (14, 14), (22, 40), (10, 44), (40, 52));
        c.Poly(accent, (60, 34), (86, 14), (78, 40), (90, 44), (60, 52));
    }

    private static void Statue(Canvas100 c, IPen pen, IPen eye)
    {
        c.Rect(pen, 30, 80, 40, 10);
        c.Poly(pen, (36, 80), (34, 40), (40, 30), (60, 30), (66, 40), (64, 80));
        c.Rect(pen, 40, 12, 20, 18);
        Eyes(c, eye, 50, 20, 5, 1.8);
        c.Line(pen, (44, 26), (56, 26));
        c.Poly(pen, (34, 44), (22, 60), (28, 64));
        c.Poly(pen, (66, 44), (78, 60), (72, 64));
        for (int i = 0; i < 4; i++) c.Line(Art.DimPen, (38, 44 + i * 9), (62, 44 + i * 9));
    }

    // ----- treasure ---------------------------------------------------------------------------

    private static void Chest(Canvas100 c)
    {
        c.Poly(Art.Line, (18, 50), (82, 50), (82, 84), (18, 84), (18, 50));
        c.Poly(Art.Line, (18, 50), (22, 36), (78, 36), (82, 50));
        c.Line(Art.GoldPen, (18, 60), (82, 60));
        c.Line(Art.GoldPen, (30, 36), (28, 84));
        c.Line(Art.GoldPen, (70, 36), (72, 84));
        c.Rect(Art.GoldPen, 45, 54, 10, 12);
        c.Circle(Art.GoldPen, 50, 59, 1.5);
        c.Line(Art.Line, (8, 86), (92, 86));
    }

    private static void Amulet(Canvas100 c)
    {
        c.Poly(Art.GoldPen, (30, 10), (50, 40), (70, 10));
        c.Circle(Art.GoldPen, 50, 56, 18);
        c.Circle(Art.AccentPen, 50, 56, 10);
        c.Circle(Art.InfoPen, 50, 56, 4);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            c.Line(Art.GoldPen, (50 + Math.Cos(a) * 22, 56 + Math.Sin(a) * 22), (50 + Math.Cos(a) * 30, 56 + Math.Sin(a) * 30));
        }
        c.Text("THE AMULET OF THAL", 50, 88, 5.5, Art.GoldBrush);
    }

    private static void Victory(Canvas100 c)
    {
        c.Text("VICTORY", 50, 40, 14, Art.GoldBrush);
    }

    /// <summary>The title's corridor: frames receding into the dark.</summary>
    private static void Corridor(DrawingContext dc, Rect bounds)
    {
        double cx = bounds.Width / 2, cy = bounds.Height / 2;
        double hw = bounds.Width / 2 - 4, hh = bounds.Height / 2 - 4;
        double[] f = [1.0, 0.64, 0.42, 0.28, 0.19, 0.13];
        Rect Frame(int d) => new(cx - hw * f[d], cy - hh * f[d], 2 * hw * f[d], 2 * hh * f[d]);
        for (int i = 0; i < f.Length - 1; i++)
        {
            var n = Frame(i);
            var fr = Frame(i + 1);
            var pen = i >= 3 ? Art.FaintLine : Art.Line;
            dc.DrawLine(pen, n.TopLeft, fr.TopLeft);
            dc.DrawLine(pen, n.BottomLeft, fr.BottomLeft);
            dc.DrawLine(pen, n.TopRight, fr.TopRight);
            dc.DrawLine(pen, n.BottomRight, fr.BottomRight);
            dc.DrawLine(pen, fr.TopLeft, fr.BottomLeft);
            dc.DrawLine(pen, fr.TopRight, fr.BottomRight);
            if (i == 1)
            {
                // A door on the right.
                double x1 = n.Right + (fr.Right - n.Right) * 0.25, x2 = n.Right + (fr.Right - n.Right) * 0.75;
                double b1 = n.Bottom + (fr.Bottom - n.Bottom) * 0.25, b2 = n.Bottom + (fr.Bottom - n.Bottom) * 0.75;
                double t1 = n.Top + (fr.Top - n.Top) * 0.25, t2 = n.Top + (fr.Top - n.Top) * 0.75;
                dc.DrawLine(Art.DoorPen, new Point(x1, b1), new Point(x1, b1 - (b1 - t1) * 0.78));
                dc.DrawLine(Art.DoorPen, new Point(x1, b1 - (b1 - t1) * 0.78), new Point(x2, b2 - (b2 - t2) * 0.78));
                dc.DrawLine(Art.DoorPen, new Point(x2, b2 - (b2 - t2) * 0.78), new Point(x2, b2));
            }
        }
        var last = Frame(f.Length - 1);
        dc.DrawLine(Art.FaintLine, last.TopLeft, last.TopRight);
        dc.DrawLine(Art.FaintLine, last.BottomLeft, last.BottomRight);
    }
}

/// <summary>Draws on a 100×100 grid mapped onto a square on screen.</summary>
internal readonly struct Canvas100(DrawingContext dc, Rect box)
{
    private Point P(double x, double y) => new(box.X + x / 100 * box.Width, box.Y + y / 100 * box.Height);
    private double S(double v) => v / 100 * box.Width;

    public void Line(IPen pen, (double X, double Y) a, (double X, double Y) b) => dc.DrawLine(Scaled(pen), P(a.X, a.Y), P(b.X, b.Y));

    public void Poly(IPen pen, params (double X, double Y)[] points)
    {
        var p = Scaled(pen);
        for (int i = 1; i < points.Length; i++)
            dc.DrawLine(p, P(points[i - 1].X, points[i - 1].Y), P(points[i].X, points[i].Y));
    }

    public void Rect(IPen pen, double x, double y, double w, double h) =>
        dc.DrawRectangle(null, Scaled(pen), new Rect(P(x, y), P(x + w, y + h)));

    public void Circle(IPen pen, double x, double y, double r) =>
        dc.DrawEllipse(null, Scaled(pen), P(x, y), S(r), S(r));

    /// <summary>A doorway: straight sides and a round top, standing on <paramref name="bottom"/>.</summary>
    public void Arch(IPen pen, double left, double bottom, double width, double height)
    {
        double r = width / 2;
        var p = Scaled(pen);
        dc.DrawLine(p, P(left, bottom), P(left, bottom - height + r));
        dc.DrawLine(p, P(left + width, bottom), P(left + width, bottom - height + r));
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(P(left, bottom - height + r), false);
            g.ArcTo(P(left + width, bottom - height + r), new Size(S(r), S(r)), 0, false, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        dc.DrawGeometry(null, p, geo);
    }

    public void Text(string text, double x, double y, double size, IBrush brush) =>
        Art.Text(dc, text, P(x, y), S(size), brush, center: true);

    /// <summary>Line widths grow a little with the picture, so big pictures don't look spidery.</summary>
    private IPen Scaled(IPen pen)
    {
        double scale = Math.Clamp(box.Width / 420, 0.8, 1.8);
        return Math.Abs(scale - 1) < 0.05 ? pen : new Pen(pen.Brush, pen.Thickness * scale, pen.DashStyle);
    }
}
