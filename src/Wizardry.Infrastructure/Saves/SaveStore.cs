using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wizardry.Core.Model;
using Wizardry.Infrastructure.Storage;

namespace Wizardry.Infrastructure.Saves;

/// <summary>What's on disk: the world, wrapped with what's needed to trust it.</summary>
public sealed class SaveFile
{
    public const string FormatId = "avawizardry-save";
    public const int CurrentSchemaVersion = 1;

    public string Format { get; set; } = FormatId;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset SavedAt { get; set; }
    public string AppVersion { get; set; } = "";
    public JsonElement World { get; set; }
    public string Checksum { get; set; } = "";
}

/// <summary>A summary for the title screen's Continue button.</summary>
public sealed record SaveSummary(DateTimeOffset SavedAt, int RosterCount, int PartyCount, int Depth, int Victories);

public sealed class SaveLoadException(string message, Exception? inner = null) : Exception(message, inner);

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(SaveFile))]
[JsonSerializable(typeof(World))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

/// <summary>
/// One world per player (the roster, the party, the maze as explored). Writes are atomic, and the
/// previous save is kept as a backup.
/// </summary>
public sealed class SaveStore(string directory)
{
    public string Directory { get; } = directory;
    public string MainPath => Path.Combine(Directory, "world.json");
    public string BackupPath => Path.Combine(Directory, "world.bak.json");

    public static SaveStore ForCurrentUser() => new(UserDataPaths.ForCurrentUser().SaveDirectory);

    public bool Exists => File.Exists(MainPath);

    public static string Serialize(World world)
    {
        var element = JsonSerializer.SerializeToElement(world, SaveJsonContext.Default.World);
        var file = new SaveFile
        {
            SavedAt = DateTimeOffset.UtcNow,
            AppVersion = typeof(SaveStore).Assembly.GetName().Version?.ToString() ?? "",
            World = element,
            Checksum = Checksum(element),
        };
        return JsonSerializer.Serialize(file, SaveJsonContext.Default.SaveFile);
    }

    public async Task SaveAsync(World world, CancellationToken ct = default)
    {
        string json = Serialize(world);
        if (File.Exists(MainPath))
        {
            try { File.Copy(MainPath, BackupPath, overwrite: true); }
            catch (IOException) { /* a missing backup is not worth failing the save */ }
        }
        await AtomicFile.WriteAllTextAsync(MainPath, json, ct);
    }

    public void Save(World world)
    {
        string json = Serialize(world);
        if (File.Exists(MainPath))
        {
            try { File.Copy(MainPath, BackupPath, overwrite: true); }
            catch (IOException) { }
        }
        AtomicFile.WriteAllText(MainPath, json);
    }

    public World Load() => Parse(ReadText(MainPath));

    public SaveSummary? Summary()
    {
        try
        {
            var file = Envelope(ReadText(MainPath));
            var w = Parse(ReadText(MainPath));
            return new SaveSummary(file.SavedAt, w.Roster.Count, w.Party.Count, w.Depth, w.Victories);
        }
        catch (SaveLoadException) { return null; }
    }

    public void Delete()
    {
        if (File.Exists(MainPath)) File.Delete(MainPath);
    }

    private static string ReadText(string path)
    {
        try { return File.ReadAllText(path); }
        catch (FileNotFoundException e) { throw new SaveLoadException("There is no saved game.", e); }
        catch (DirectoryNotFoundException e) { throw new SaveLoadException("There is no saved game.", e); }
        catch (IOException e) { throw new SaveLoadException("The save file could not be read: " + e.Message, e); }
        catch (UnauthorizedAccessException e) { throw new SaveLoadException("Permission denied reading the save file.", e); }
    }

    private static SaveFile Envelope(string json)
    {
        SaveFile? file;
        try { file = JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveFile); }
        catch (JsonException e) { throw new SaveLoadException("The save file is damaged (not valid save data).", e); }
        if (file is null || file.Format != SaveFile.FormatId)
            throw new SaveLoadException("This is not an AVAWizardry save file.");
        if (file.SchemaVersion > SaveFile.CurrentSchemaVersion)
            throw new SaveLoadException($"This save was made by a newer version (format {file.SchemaVersion}). Please update the game.");
        if (file.World.ValueKind != JsonValueKind.Object || Checksum(file.World) != file.Checksum)
            throw new SaveLoadException("The save file is damaged (checksum mismatch).");
        return file;
    }

    public static World Parse(string json)
    {
        var file = Envelope(json);
        World? world;
        try { world = file.World.Deserialize(SaveJsonContext.Default.World); }
        catch (JsonException e) { throw new SaveLoadException("The save file is damaged: " + e.Message, e); }
        if (world is null) throw new SaveLoadException("The save file is empty.");
        // Party members must exist in the roster.
        world.Party.RemoveAll(id => world.Find(id) is null);
        foreach (var c in world.Roster)
        {
            c.Items.RemoveAll(i => !Wizardry.Core.Content.Items.Exists(i.ItemId));
            if (c.Stats.Length != 6) throw new SaveLoadException($"The save file is damaged ({c.Name}'s attributes).");
            if (c.MageSlots.Length != 7) c.MageSlots = new int[7];
            if (c.PriestSlots.Length != 7) c.PriestSlots = new int[7];
        }
        return world;
    }

    private static string Checksum(JsonElement element) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(element))).ToLowerInvariant();
}
