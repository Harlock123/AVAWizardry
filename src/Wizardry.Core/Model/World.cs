using Wizardry.Core.Content;
using Wizardry.Core.Maze;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Model;

/// <summary>Rule choices fixed when a game starts.</summary>
public sealed class GameOptions
{
    /// <summary>
    /// When a whole party falls, a patrol carries the bodies to the temple. Off (the classic rule):
    /// they stay where they fell until another party recovers them.
    /// </summary>
    public bool RescuePatrols { get; set; } = true;
}

/// <summary>Everything a save holds: the roster, the party, where it stands and what it has learned.</summary>
public sealed class World
{
    public const int MaxRoster = 20;
    public const int MaxParty = 6;

    public int Version { get; set; } = 1;
    public ulong RngState { get; set; } = 1;
    public GameOptions Options { get; set; } = new();
    public List<Character> Roster { get; set; } = [];
    /// <summary>Ids of the party's members, front rank first.</summary>
    public List<string> Party { get; set; } = [];

    // Where the party is. Depth 0 means the castle.
    public int Depth { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Dir Facing { get; set; } = Dir.North;

    /// <summary>Steps of light left (LUMA); <see cref="LongLight"/> lasts the whole trip.</summary>
    public int LightSteps { get; set; }
    public bool LongLight { get; set; }
    /// <summary>Armour bonus for the whole party until it leaves the maze (OMSKUTA).</summary>
    public int TripWard { get; set; }

    /// <summary>Squares each level's map shows, as bit strings ("1" = seen), index depth - 1.</summary>
    public List<string> Explored { get; set; } = [];
    /// <summary>Rooms cleared this trip, as "depth:room". Emptied when the party leaves the maze.</summary>
    public HashSet<string> ClearedRooms { get; set; } = [];
    /// <summary>The Warden has been beaten at least once (it stands guard again if its ribbon is lost).</summary>
    public bool WardenDefeated { get; set; }
    /// <summary>Morthrain has fallen at least once.</summary>
    public bool MorthrainDefeated { get; set; }
    /// <summary>Victories: times the amulet has come home.</summary>
    public int Victories { get; set; }
    /// <summary>Weeks since the game began (time passes at the inn).</summary>
    public int Week { get; set; }
    /// <summary>Trading post stock beyond the never-ending items, by item id.</summary>
    public Dictionary<int, int> Stock { get; set; } = [];
    /// <summary>Recent messages, newest last.</summary>
    public List<string> Log { get; set; } = [];

    public IEnumerable<Character> Members => Party.Select(Find).OfType<Character>();

    public Character? Find(string id) => Roster.FirstOrDefault(c => c.Id == id);

    public bool InMaze => Depth > 0;

    public bool PartyHas(int itemId) => Members.Any(m => m.Items.Any(i => i.ItemId == itemId));

    public bool IsExplored(int depth, int x, int y)
    {
        if (depth < 1 || depth > Explored.Count) return false;
        return Explored[depth - 1][y * Level.Size + x] == '1';
    }

    public void MarkExplored(int depth, int x, int y)
    {
        while (Explored.Count < MazeBuilder.Depth) Explored.Add(new string('0', Level.Size * Level.Size));
        var row = Explored[depth - 1].ToCharArray();
        row[y * Level.Size + x] = '1';
        Explored[depth - 1] = new string(row);
    }

    public void Say(string text)
    {
        Log.Add(text);
        if (Log.Count > 200) Log.RemoveRange(0, Log.Count - 200);
    }

    public static World NewGame(ulong seed, GameOptions? options = null)
    {
        var world = new World { RngState = seed, Options = options ?? new GameOptions() };
        foreach (var item in Items.All.Where(i => i.Stock > 0))
            world.Stock[item.Id] = item.Stock;
        return world;
    }
}
