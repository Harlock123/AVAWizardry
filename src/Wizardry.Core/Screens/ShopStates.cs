using Wizardry.Core.Content;
using Wizardry.Core.Model;

namespace Wizardry.Core.Screens;

/// <summary>Harrow's: buys, sells, identifies and lifts curses. One customer at a time.</summary>
internal sealed class ShopState(Game game) : State(game)
{
    public override Screen Render() => new()
    {
        Title = "HARROW'S TRADING POST",
        Place = "shop",
        Lines =
        [
            "Weapons on the walls, armour on the racks, and Harrow himself",
            "behind the counter, weighing coins.",
            "",
            new("\"Who's buying?\"", Tone.Dim),
        ],
        Options = [new("C", "Choose a customer", G.Party.Count > 0), new("ESC", "Leave")],
    };

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        G.Push(new PickState(G, "WHO IS BUYING?", "Who will shop?", G.Party, c => G.Push(new CustomerState(G, c)),
            c => $"{c.Name,-15} {c.ClassTag}  {Money(c.Gold)}", place: "shop"));
    }

    public static long SellPrice(ItemDef def) => Math.Max(1, def.Price / 2);
    public static long IdentifyPrice(ItemDef def) => Math.Max(1, def.Price / 2);
    public static long UncursePrice(ItemDef def) => Math.Max(1, def.Price / 2);
}

internal sealed class CustomerState(Game game, Character who) : State(game)
{
    public override Screen Render() => new()
    {
        Title = "HARROW'S TRADING POST",
        Place = "shop",
        Lines =
        [
            $"Welcome, {who.Name}. You have {Money(who.Gold)} and carry {who.Items.Count} of {Character.MaxItems} items.",
        ],
        Options =
        [
            new("B", "Buy", who.Items.Count < Character.MaxItems),
            new("S", "Sell", who.Items.Any(i => !i.Stuck && i.Def.Kind != ItemKind.Special)),
            new("I", "Identify", who.Items.Any(i => !i.Identified)),
            new("U", "Uncurse", who.Items.Any(i => i.Stuck)),
            new("P", "Pool the party's gold", G.Party.Count > 1),
            new("ESC", "Leave"),
        ],
    };

    public override void Key(string key)
    {
        switch (key)
        {
            case "B": G.Push(new BuyState(G, who)); break;
            case "S":
                G.Push(new ItemPickState(G, who, "SELL", "Sell which item?", i => !i.Stuck && i.Def.Kind != ItemKind.Special,
                    i => $"{ShopState.SellPrice(i.Def):N0} GP", item =>
                    {
                        long price = ShopState.SellPrice(item.Def);
                        who.Items.Remove(item);
                        who.Gold += price;
                        if (item.Def.Kind != ItemKind.Special && item.Def.Stock != -1)
                            W.Stock[item.ItemId] = W.Stock.GetValueOrDefault(item.ItemId) + 1;
                        G.Play(SoundCue.Coins);
                        G.Tell("SOLD", [$"Harrow pays {price:N0} GP for the {item.DisplayName}."]);
                    }));
                break;
            case "I":
                G.Push(new ItemPickState(G, who, "IDENTIFY", "Identify which item?", i => !i.Identified,
                    i => $"{ShopState.IdentifyPrice(i.Def):N0} GP", item =>
                    {
                        long price = ShopState.IdentifyPrice(item.Def);
                        if (who.Gold < price) { G.Tell("IDENTIFY", [new("\"You can't afford that, friend.\"", Tone.Bad)]); return; }
                        who.Gold -= price;
                        item.Identified = true;
                        G.Play(SoundCue.Coins);
                        G.Tell("IDENTIFY", [$"Harrow squints at it. \"That's a {item.Def.Name}.\"", item.Def.Notes]);
                    }));
                break;
            case "U":
                G.Push(new ItemPickState(G, who, "UNCURSE", "Uncurse which item?", i => i.Stuck,
                    i => $"{ShopState.UncursePrice(i.Def):N0} GP", item =>
                    {
                        long price = ShopState.UncursePrice(item.Def);
                        if (who.Gold < price) { G.Tell("UNCURSE", [new("\"You can't afford that, friend.\"", Tone.Bad)]); return; }
                        who.Gold -= price;
                        item.Equipped = false;
                        item.Identified = true;
                        G.Play(SoundCue.Coins);
                        G.Tell("UNCURSE", [$"The curse is broken and the {item.Def.Name} falls from {who.Name}'s grip.", "(It is still cursed, should anyone be foolish enough to use it again.)"]);
                    }));
                break;
            case "P":
                G.PoolGold(who);
                G.Tell("POOLED", [$"{who.Name} now holds the party's {Money(who.Gold)}."]);
                break;
            case "ESC":
                G.Pop();
                break;
        }
    }
}

/// <summary>The stock, a page at a time.</summary>
internal sealed class BuyState(Game game, Character who) : State(game)
{
    private const int PageSize = 9;
    private int _page;

    private List<ItemDef> Stock() => Items.All
        .Where(i => i.Kind != ItemKind.Special && (i.Stock == -1 || W.Stock.GetValueOrDefault(i.Id) > 0))
        .OrderBy(i => i.Kind).ThenBy(i => i.Price)
        .ToList();

    public override Screen Render()
    {
        var stock = Stock();
        int pages = Math.Max(1, (stock.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        var lines = new List<Line> { $"{who.Name} has {Money(who.Gold)}.  (* = {who.Name} can't use it)", "" };
        var options = new List<MenuOption>();
        var page = stock.Skip(_page * PageSize).Take(PageSize).ToList();
        for (int i = 0; i < page.Count; i++)
        {
            var item = page[i];
            string count = item.Stock == -1 ? "" : $" ({W.Stock.GetValueOrDefault(item.Id)} left)";
            lines.Add(new($"{i + 1}) {(item.UsableBy(who.Class) ? " " : "*")}{item.Name,-22}{item.Price,9:N0} GP{count}", item.Price <= who.Gold ? Tone.Normal : Tone.Dim));
            options.Add(new(Keyed(i), item.Name, item.Price <= who.Gold && who.Items.Count < Character.MaxItems));
        }
        lines.Add("");
        lines.Add(new($"Page {_page + 1} of {pages}", Tone.Dim));
        options.Add(new("N", "Next page", _page < pages - 1));
        options.Add(new("B", "Previous page", _page > 0));
        options.Add(new("ESC", "Done"));
        return new Screen { Title = "BUY", Place = "shop", Lines = lines, Options = options };
    }

    public override void Key(string key)
    {
        switch (key)
        {
            case "N": _page++; return;
            case "B": _page--; return;
            case "ESC": G.Pop(); return;
        }
        var page = Stock().Skip(_page * PageSize).Take(PageSize).ToList();
        if (IndexOf(key) is not { } i || i >= page.Count) return;
        var item = page[i];
        who.Gold -= item.Price;
        who.Items.Add(OwnedItem.Known(item.Id));
        if (item.Stock != -1) W.Stock[item.Id] = W.Stock.GetValueOrDefault(item.Id) - 1;
        G.Play(SoundCue.Coins);
        G.Say($"{who.Name} buys a {item.Name}.");
        var lines = new List<Line> { $"\"A fine {item.Name}. Anything else?\"" };
        if (!item.UsableBy(who.Class)) lines.Add(new($"(A {who.Class.ToString().ToLowerInvariant()} can't use it.)", Tone.Dim));
        else if (item.Equippable) lines.Add(new("(Equip it from the character's sheet: Tavern > Inspect, or Camp in the maze.)", Tone.Dim));
        G.Tell("SOLD!", lines);
    }
}

/// <summary>Picks one of a character's items.</summary>
internal sealed class ItemPickState(Game game, Character who, string title, string question, Func<OwnedItem, bool> allowed,
    Func<OwnedItem, string>? note, Action<OwnedItem> picked, Pane pane = Pane.Castle, string place = "shop") : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { question, "" };
        var options = new List<MenuOption>();
        for (int i = 0; i < who.Items.Count; i++)
        {
            var item = who.Items[i];
            bool ok = allowed(item);
            lines.Add(new($"{i + 1}) {SheetState.ItemLine(who, item)}{(note is null ? "" : "  " + note(item))}", ok ? Tone.Normal : Tone.Dim));
            options.Add(new(Keyed(i), item.DisplayName, ok));
        }
        options.Add(new("ESC", "Back"));
        return new Screen { Title = title, Lines = lines, Options = options, Pane = pane, Place = place };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        if (IndexOf(key) is { } i && i < who.Items.Count)
        {
            G.Pop();
            picked(who.Items[i]);
        }
    }
}
