using Wizardry.Core.Rules;

namespace Wizardry.Core.Maze;

/// <summary>
/// Builds the ten levels of the Proving Grounds. The maze is the same in every game: each level
/// is carved from its own fixed seed, and each starts where the stairs from the level above come
/// down. Every square of a level can be reached from its stairs.
/// </summary>
public static class MazeBuilder
{
    public const int Depth = 10;
    private const ulong Seed = 0x5741_5A31_3938_31UL; // fixed: the maze never changes

    private static readonly Lazy<IReadOnlyList<Level>> Built = new(BuildAll);

    /// <summary>The ten levels, index 0 = level 1.</summary>
    public static IReadOnlyList<Level> Levels => Built.Value;

    public static Level Get(int depth) => Levels[depth - 1];

    private static IReadOnlyList<Level> BuildAll()
    {
        var levels = new List<Level>();
        (int X, int Y) start = (0, 0);
        for (int depth = 1; depth <= Depth; depth++)
        {
            var level = Build(depth, start);
            levels.Add(level);
            if (level.StairsDown is { } down) start = down;
        }
        AddLift(levels, 1, 4, needsRibbon: false, near: (2, 2));
        AddLift(levels, 4, 9, needsRibbon: true, near: (12, 12));
        return levels;
    }

    internal static Level Build(int depth, (int X, int Y) start)
    {
        var rng = new Rng(Seed + (ulong)depth * 0x9E37_79B9UL);
        var level = new Level(depth) { StairsUp = start };
        const int n = Level.Size;

        // 1. A perfect maze by depth-first carving from the arrival square.
        var visited = new bool[n, n];
        var stack = new Stack<(int X, int Y)>();
        stack.Push(start);
        visited[start.X, start.Y] = true;
        var dist = new int[n, n];
        while (stack.Count > 0)
        {
            var (x, y) = stack.Peek();
            var options = Enum.GetValues<Dir>()
                .Where(d => Level.InBounds(x + d.Dx(), y + d.Dy()) && !visited[x + d.Dx(), y + d.Dy()])
                .ToList();
            if (options.Count == 0) { stack.Pop(); continue; }
            var dir = rng.Pick(options);
            int nx = x + dir.Dx(), ny = y + dir.Dy();
            level.SetEdge(x, y, dir, Edge.Open);
            visited[nx, ny] = true;
            dist[nx, ny] = dist[x, y] + 1;
            stack.Push((nx, ny));
        }

        // 2. The way down (or the lair) is the dead end farthest from the arrival.
        (int X, int Y) far = start;
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
                if (dist[x, y] > dist[far.X, far.Y] && Exits(level, x, y) == 1)
                    far = (x, y);

        var reserved = new HashSet<(int, int)> { start, far };
        bool lairLevel = depth is 4 or Depth;

        // 3. Rooms: open blocks with doors where corridors meet them.
        int rooms = depth == 1 ? 3 : 4 + depth / 3;
        int roomId = 0;
        for (int attempt = 0; attempt < 200 && roomId < rooms; attempt++)
        {
            int w = rng.Range(2, 3), h = rng.Range(2, 3);
            int rx = rng.Range(0, n - w), ry = rng.Range(0, n - h);
            bool clash = false;
            for (int x = rx - 1; x <= rx + w && !clash; x++)
                for (int y = ry - 1; y <= ry + h && !clash; y++)
                    if (Level.InBounds(x, y) && (level.RoomAt(x, y) != 0 || reserved.Contains((x, y)) || Near(x, y, far, 2) || Near(x, y, start, 1)))
                        clash = true;
            if (clash) continue;
            roomId++;
            CarveRoom(level, rx, ry, w, h, roomId);
        }
        level.RoomCount = roomId;

        // 4. A few loops, so the maze isn't a single tree (never next to the far square).
        int loops = 18 + depth * 2;
        for (int i = 0; i < loops * 4 && loops > 0; i++)
        {
            int x = rng.Next(n), y = rng.Next(n);
            var d = rng.OneIn(2) ? Dir.East : Dir.North;
            int nx = x + d.Dx(), ny = y + d.Dy();
            if (!Level.InBounds(nx, ny) || level.EdgeAt(x, y, d) != Edge.Wall) continue;
            if (Near(x, y, far, 1) || Near(nx, ny, far, 1)) continue;
            if (level.RoomAt(x, y) != 0 || level.RoomAt(nx, ny) != 0) continue;
            level.SetEdge(x, y, d, rng.OneIn(5) ? Edge.Secret : Edge.Open);
            loops--;
        }

        // 5. Doors and secret doors along the corridors.
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
                foreach (var d in new[] { Dir.East, Dir.North })
                {
                    int nx = x + d.Dx(), ny = y + d.Dy();
                    if (!Level.InBounds(nx, ny) || level.EdgeAt(x, y, d) != Edge.Open) continue;
                    if (level.RoomAt(x, y) != 0 && level.RoomAt(x, y) == level.RoomAt(nx, ny)) continue;
                    if (rng.Percent(14)) level.SetEdge(x, y, d, Edge.Door);
                    else if (depth > 1 && rng.Percent(3 + depth / 2)) level.SetEdge(x, y, d, Edge.Secret);
                }

        // 6. The far square: stairs down, or a lair.
        if (depth == Depth)
        {
            level.LairCenter = far;
            level.SetFeature(far.X, far.Y, Feature.Lair);
            SealOnlyExit(level, far, Edge.Door);
        }
        else
        {
            level.StairsDown = far;
            level.SetFeature(far.X, far.Y, Feature.StairsDown);
            if (depth == 4) SealOnlyExit(level, far, Edge.Sealed);
        }
        level.SetFeature(start.X, start.Y, Feature.StairsUp);

        // The Warden guards the ribbon in a lair of its own on level 4: a far-off dead end.
        if (depth == 4)
        {
            var bfs = Distances(level, start);
            (int X, int Y) best = start;
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    if (bfs[x, y] > bfs[best.X, best.Y] && level.FeatureAt(x, y) == Feature.None
                        && level.RoomAt(x, y) == 0 && !Near(x, y, far, 3) && Exits(level, x, y) == 1)
                        best = (x, y);
            level.LairCenter = best;
            level.SetFeature(best.X, best.Y, Feature.Lair);
            reserved.Add(best);
        }

        // 7. Hazards and oddities, more of them deeper down.
        var free = new List<(int X, int Y)>();
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
                if (level.FeatureAt(x, y) == Feature.None && !reserved.Contains((x, y)) && !Near(x, y, start, 1) && !Near(x, y, far, 1))
                    free.Add((x, y));
        void Scatter(Feature f, int count)
        {
            for (int i = 0; i < count && free.Count > 0; i++)
            {
                var p = free[rng.Next(free.Count)];
                free.Remove(p);
                level.SetFeature(p.X, p.Y, f);
            }
        }
        // Dark patches come in small clumps.
        int darkClumps = depth switch { 1 => 1, <= 3 => 2, <= 6 => 3, _ => 4 };
        for (int c = 0; c < darkClumps && free.Count > 0; c++)
        {
            var seed = free[rng.Next(free.Count)];
            foreach (var p in free.Where(p => Math.Abs(p.X - seed.X) + Math.Abs(p.Y - seed.Y) <= 1 + rng.Next(2)).ToList())
            {
                free.Remove(p);
                level.SetFeature(p.X, p.Y, Feature.Dark);
            }
        }
        Scatter(Feature.Spinner, depth == 1 ? 1 : 2 + depth / 3);
        Scatter(Feature.Pit, depth == 1 ? 1 : 2 + depth / 2);
        if (depth >= 3) Scatter(Feature.Teleporter, 1 + depth / 4);
        if (depth >= 5 && depth < Depth) Scatter(Feature.Chute, 1);
        if (depth >= 7) Scatter(Feature.AntiMagic, 2 + (depth - 7));

        // 8. Words on the walls.
        foreach (var text in WallWords(depth))
        {
            if (free.Count == 0) break;
            var p = free[rng.Next(free.Count)];
            free.Remove(p);
            level.SetMessage(p.X, p.Y, text);
        }
        return level;
    }

    /// <summary>
    /// A lift shaft: the same square on each level it serves, chosen near <paramref name="near"/>
    /// where every one of those levels has nothing else.
    /// </summary>
    private static void AddLift(List<Level> levels, int low, int high, bool needsRibbon, (int X, int Y) near)
    {
        var spots = new List<(int X, int Y)>();
        for (int x = 0; x < Level.Size; x++)
            for (int y = 0; y < Level.Size; y++)
                spots.Add((x, y));
        foreach (var (x, y) in spots.OrderBy(p => Math.Abs(p.X - near.X) + Math.Abs(p.Y - near.Y)))
        {
            bool clear = true;
            for (int d = low; d <= high && clear; d++)
            {
                var l = levels[d - 1];
                clear = l.FeatureAt(x, y) == Feature.None && l.RoomAt(x, y) == 0
                    && !Enum.GetValues<Dir>().Any(dir => l.FeatureAt(x + dir.Dx(), y + dir.Dy()) is Feature.StairsUp or Feature.StairsDown or Feature.Lair);
            }
            if (!clear) continue;
            for (int d = low; d <= high; d++)
            {
                levels[d - 1].SetFeature(x, y, Feature.Lift);
                levels[d - 1].Lifts.Add(new LiftStop(x, y, low, high, needsRibbon));
            }
            return;
        }
    }

    private static void CarveRoom(Level level, int rx, int ry, int w, int h, int id)
    {
        for (int x = rx; x < rx + w; x++)
            for (int y = ry; y < ry + h; y++)
            {
                level.SetRoom(x, y, id);
                level.SetFeature(x, y, Feature.Room);
                if (x + 1 < rx + w) level.SetEdge(x, y, Dir.East, Edge.Open);
                if (y + 1 < ry + h) level.SetEdge(x, y, Dir.North, Edge.Open);
            }
        // Passages that already led into the block become doors.
        for (int x = rx; x < rx + w; x++)
            for (int y = ry; y < ry + h; y++)
                foreach (var d in Enum.GetValues<Dir>())
                {
                    int nx = x + d.Dx(), ny = y + d.Dy();
                    if (!Level.InBounds(nx, ny) || level.RoomAt(nx, ny) == id) continue;
                    if (level.EdgeAt(x, y, d) == Edge.Open) level.SetEdge(x, y, d, Edge.Door);
                }
    }

    private static void SealOnlyExit(Level level, (int X, int Y) at, Edge kind)
    {
        foreach (var d in Enum.GetValues<Dir>())
            if (level.EdgeAt(at.X, at.Y, d) != Edge.Wall)
                level.SetEdge(at.X, at.Y, d, kind);
    }

    private static int Exits(Level level, int x, int y) =>
        Enum.GetValues<Dir>().Count(d => level.EdgeAt(x, y, d) != Edge.Wall);

    private static bool Near(int x, int y, (int X, int Y) p, int r) => Math.Abs(x - p.X) <= r && Math.Abs(y - p.Y) <= r;

    private static int[,] Distances(Level level, (int X, int Y) from)
    {
        var dist = new int[Level.Size, Level.Size];
        for (int x = 0; x < Level.Size; x++)
            for (int y = 0; y < Level.Size; y++)
                dist[x, y] = -1;
        dist[from.X, from.Y] = 0;
        var q = new Queue<(int X, int Y)>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                if (!level.CanPass(x, y, d, hasRibbon: false)) continue;
                int nx = x + d.Dx(), ny = y + d.Dy();
                if (dist[nx, ny] >= 0) continue;
                dist[nx, ny] = dist[x, y] + 1;
                q.Enqueue((nx, ny));
            }
        }
        return dist;
    }

    private static string[] WallWords(int depth) => depth switch
    {
        1 => ["SCRAWLED ON THE WALL: \"THE OVERLORD'S GOLD BUYS NO SECOND LIFE.\"",
              "A NOTICE: \"TRAINEES WILL NOT BE RESCUED BEYOND THIS POINT.\""],
        2 => ["CARVED HERE: \"MAP EVERY STEP. THE WALLS LIE.\""],
        3 => ["A SKELETAL HAND POINTS NORTH. ITS OWNER DID NOT FOLLOW IT."],
        4 => ["ETCHED IN IRON: \"THE WARDEN KEEPS THE WAY. ITS RIBBON OPENS IT.\"",
              "SOMEONE HAS WRITTEN: \"TURN BACK.\" SOMEONE ELSE: \"COWARD.\""],
        5 => ["THE STONE HERE IS WARM, AS IF SOMETHING BELOW BREATHES."],
        6 => ["A WARNING IN RED: \"THE FLOOR FALLS AWAY WHERE YOU LEAST EXPECT.\""],
        7 => ["THE AIR TASTES OF COPPER. MAGIC FEELS THIN HERE."],
        8 => ["SCRATCHED BY MANY HANDS: \"MORTHRAIN SEES.\""],
        9 => ["A FADED SIGIL OF THAL. IT GLOWS FAINTLY AS YOU PASS."],
        _ => ["LETTERS OF FIRE: \"BRING ME NO AMULET-SEEKERS. BRING ME THEIR BONES.\" - M."],
    };
}
