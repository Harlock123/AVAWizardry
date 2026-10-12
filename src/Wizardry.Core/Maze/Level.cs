namespace Wizardry.Core.Maze;

/// <summary>Compass directions in turning order: right turns add one.</summary>
public enum Dir { North, East, South, West }

/// <summary>What lies along one side of a square.</summary>
public enum Edge
{
    Open,
    Wall,
    Door,
    /// <summary>Looks like a wall until found (walking into it, or by light); always passable.</summary>
    Secret,
    /// <summary>A door that opens only for a party carrying the Warden's Ribbon.</summary>
    Sealed,
}

public enum Feature
{
    None,
    StairsUp,
    StairsDown,
    /// <summary>Nothing can be seen here, not even with light.</summary>
    Dark,
    /// <summary>Turns the party to face a random way, without telling them.</summary>
    Spinner,
    /// <summary>Moves the party to a random square on the level.</summary>
    Teleporter,
    /// <summary>Wounds everyone who falls in.</summary>
    Pit,
    /// <summary>Drops the party to the level below.</summary>
    Chute,
    /// <summary>Spells cast here fizzle.</summary>
    AntiMagic,
    /// <summary>Words on the wall (see <see cref="Level.Messages"/>).</summary>
    Message,
    /// <summary>A square of a room: a guarded chest waits on the first visit each trip.</summary>
    Room,
    /// <summary>A fixed fight with a unique guardian (the Warden on 4, Morthrain on 10).</summary>
    Lair,
    /// <summary>A lift between levels (1-4; and 4-9 for those who carry the ribbon).</summary>
    Lift,
}

public static class DirExtensions
{
    public static Dir Right(this Dir d) => (Dir)(((int)d + 1) & 3);
    public static Dir Left(this Dir d) => (Dir)(((int)d + 3) & 3);
    public static Dir Back(this Dir d) => (Dir)(((int)d + 2) & 3);
    public static int Dx(this Dir d) => d switch { Dir.East => 1, Dir.West => -1, _ => 0 };
    /// <summary>North is +y, as on the classic graph-paper maps.</summary>
    public static int Dy(this Dir d) => d switch { Dir.North => 1, Dir.South => -1, _ => 0 };
    public static char Letter(this Dir d) => "NESW"[(int)d];
}

/// <summary>A lift stop on a level: the lift serves levels <see cref="Low"/> to <see cref="High"/>.</summary>
public sealed record LiftStop(int X, int Y, int Low, int High, bool NeedsRibbon);

/// <summary>One 20×20 level of the maze. (0,0) is the south-west corner; north is +y.</summary>
public sealed class Level
{
    public const int Size = 20;

    public Level(int depth)
    {
        Depth = depth;
        for (int i = 0; i < _east.Length; i++) { _east[i] = Edge.Wall; _north[i] = Edge.Wall; }
    }

    public int Depth { get; }

    // _east[x,y] is the edge between (x,y) and (x+1,y); _north[x,y] between (x,y) and (x,y+1).
    private readonly Edge[] _east = new Edge[Size * Size];
    private readonly Edge[] _north = new Edge[Size * Size];
    private readonly Feature[] _features = new Feature[Size * Size];
    private readonly int[] _rooms = new int[Size * Size];

    /// <summary>Wall messages by square index (y * Size + x).</summary>
    public Dictionary<int, string> Messages { get; } = [];

    public (int X, int Y) StairsUp { get; internal set; }
    public (int X, int Y)? StairsDown { get; internal set; }
    public (int X, int Y)? LairCenter { get; internal set; }
    /// <summary>Where this level's lifts stop, and the levels each one serves.</summary>
    public List<LiftStop> Lifts { get; } = [];
    public int RoomCount { get; internal set; }

    public static bool InBounds(int x, int y) => x is >= 0 and < Size && y is >= 0 and < Size;

    private static int Index(int x, int y) => y * Size + x;

    public Edge EdgeAt(int x, int y, Dir d)
    {
        int nx = x + d.Dx(), ny = y + d.Dy();
        if (!InBounds(x, y) || !InBounds(nx, ny)) return Edge.Wall;
        return d switch
        {
            Dir.East => _east[Index(x, y)],
            Dir.West => _east[Index(nx, ny)],
            Dir.North => _north[Index(x, y)],
            _ => _north[Index(nx, ny)],
        };
    }

    internal void SetEdge(int x, int y, Dir d, Edge e)
    {
        int nx = x + d.Dx(), ny = y + d.Dy();
        if (!InBounds(x, y) || !InBounds(nx, ny)) return;
        switch (d)
        {
            case Dir.East: _east[Index(x, y)] = e; break;
            case Dir.West: _east[Index(nx, ny)] = e; break;
            case Dir.North: _north[Index(x, y)] = e; break;
            default: _north[Index(nx, ny)] = e; break;
        }
    }

    public Feature FeatureAt(int x, int y) => InBounds(x, y) ? _features[Index(x, y)] : Feature.None;

    internal void SetFeature(int x, int y, Feature f) => _features[Index(x, y)] = f;

    /// <summary>Room number (1-based) of a room square, else 0.</summary>
    public int RoomAt(int x, int y) => InBounds(x, y) ? _rooms[Index(x, y)] : 0;

    internal void SetRoom(int x, int y, int room) => _rooms[Index(x, y)] = room;

    public string? MessageAt(int x, int y) => Messages.GetValueOrDefault(Index(x, y));

    internal void SetMessage(int x, int y, string text)
    {
        Messages[Index(x, y)] = text;
        SetFeature(x, y, Feature.Message);
    }

    /// <summary>Can a party walk from (x,y) in direction d (walls stop it; sealed doors need the ribbon)?</summary>
    public bool CanPass(int x, int y, Dir d, bool hasRibbon) => EdgeAt(x, y, d) switch
    {
        Edge.Open or Edge.Door or Edge.Secret => true,
        Edge.Sealed => hasRibbon,
        _ => false,
    };

    /// <summary>Squares reachable from (x,y), walking through every passable edge.</summary>
    public HashSet<(int X, int Y)> Reachable(int x, int y, bool hasRibbon)
    {
        var seen = new HashSet<(int, int)> { (x, y) };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                if (!CanPass(cx, cy, d, hasRibbon)) continue;
                var n = (cx + d.Dx(), cy + d.Dy());
                if (seen.Add(n)) queue.Enqueue(n);
            }
        }
        return seen;
    }
}
