using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wizardry.Core.Content;
using Wizardry.Core.Rules;

namespace Wizardry.Desktop.ViewModels;

public enum BlockKind { Heading, Subheading, Paragraph, Bullet, Code }

/// <summary>A paragraph, heading or list item of a help page.</summary>
public sealed record HelpBlock(BlockKind Kind, string Text)
{
    public double Size => Kind switch { BlockKind.Heading => 34, BlockKind.Subheading => 24, BlockKind.Code => 19, _ => 15 };
    public bool IsRetro => Kind is BlockKind.Heading or BlockKind.Subheading or BlockKind.Code;
    public string Shown => Kind == BlockKind.Bullet ? "•  " + Text : Text;
    public Avalonia.Thickness Margin => Kind switch
    {
        BlockKind.Heading => new(0, 0, 0, 6),
        BlockKind.Subheading => new(0, 14, 0, 4),
        BlockKind.Bullet => new(14, 1, 0, 1),
        _ => new(0, 4, 0, 4),
    };
}

public sealed record HelpPage(string Title, IReadOnlyList<HelpBlock> Blocks)
{
    public override string ToString() => Title;
}

/// <summary>The help pages: Markdown files under Assets/Help, plus the spell list built from the game's data.</summary>
public sealed partial class HelpViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public HelpViewModel(MainViewModel main)
    {
        _main = main;
        var pages = LoadPages();
        pages.Insert(Math.Min(4, pages.Count), SpellPage());
        Pages = pages;
        _selected = pages.FirstOrDefault();
    }

    public IReadOnlyList<HelpPage> Pages { get; }

    [ObservableProperty] private HelpPage? _selected;

    [RelayCommand] private void Back() => _main.Back();

    private static List<HelpPage> LoadPages()
    {
        var pages = new List<HelpPage>();
        var root = new Uri("avares://AVAWizardry/Assets/Help/");
        foreach (var uri in AssetLoader.GetAssets(root, null).OrderBy(u => u.AbsolutePath))
        {
            if (!uri.AbsolutePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            pages.Add(Parse(reader.ReadToEnd()));
        }
        return pages;
    }

    /// <summary>Just enough Markdown: # and ## headings, - bullets, ``` blocks and paragraphs.</summary>
    public static HelpPage Parse(string markdown)
    {
        var blocks = new List<HelpBlock>();
        string title = "Help";
        var para = new List<string>();
        bool code = false;
        var codeLines = new List<string>();
        void Flush()
        {
            if (para.Count > 0) blocks.Add(new(BlockKind.Paragraph, string.Join(" ", para)));
            para.Clear();
        }
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("```"))
            {
                if (code) { blocks.Add(new(BlockKind.Code, string.Join("\n", codeLines))); codeLines.Clear(); }
                else Flush();
                code = !code;
                continue;
            }
            if (code) { codeLines.Add(line); continue; }
            if (line.StartsWith("# ")) { Flush(); title = line[2..].Trim(); blocks.Add(new(BlockKind.Heading, title)); }
            else if (line.StartsWith("## ")) { Flush(); blocks.Add(new(BlockKind.Subheading, line[3..].Trim())); }
            else if (line.StartsWith("- ") || line.StartsWith("* ")) { Flush(); blocks.Add(new(BlockKind.Bullet, Strip(line[2..].Trim()))); }
            else if (line.StartsWith("  ") && blocks.Count > 0 && blocks[^1].Kind == BlockKind.Bullet && para.Count == 0)
                blocks[^1] = blocks[^1] with { Text = blocks[^1].Text + " " + Strip(line.Trim()) };
            else if (line.Length == 0) Flush();
            else para.Add(Strip(line.Trim()));
        }
        Flush();
        return new HelpPage(title, blocks);
    }

    /// <summary>Removes Markdown emphasis and code marks; the help is plain text.</summary>
    private static string Strip(string s) => s.Replace("**", "").Replace("`", "");

    private static HelpPage SpellPage()
    {
        var blocks = new List<HelpBlock>
        {
            new(BlockKind.Heading, "Spells"),
            new(BlockKind.Paragraph, "Mages, bishops and samurai learn mage spells; priests, bishops and lords learn priest spells. Each spell level has its own number of casts, restored by a night at the inn (even in the stables). A character always has at least one cast for each spell they know at a level."),
        };
        foreach (var school in new[] { School.Mage, School.Priest })
        {
            blocks.Add(new(BlockKind.Subheading, school == School.Mage ? "Mage spells" : "Priest spells"));
            foreach (var s in Spells.All.Where(s => s.School == school))
            {
                string where = s.InCombat && s.InCamp ? "combat and camp" : s.InCamp ? "camp only" : "combat only";
                blocks.Add(new(BlockKind.Bullet, $"Level {s.Level}  {s.Name}: {s.Summary} ({where})"));
            }
        }
        return new HelpPage("Spells", blocks);
    }
}
