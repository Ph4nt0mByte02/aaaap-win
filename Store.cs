using System.Text.Json;
namespace MyProgress;
public class Col { public string id { get; set; } = Store.Uid(); public string? key { get; set; } public string? name { get; set; } public string icon { get; set; } = "📖"; }
public class Item { public string id { get; set; } = Store.Uid(); public string columnId { get; set; } = ""; public string number { get; set; } = "01"; public string chapter { get; set; } = ""; public bool done { get; set; } public string? doneDate { get; set; } }
public class Subject { public string id { get; set; } = Store.Uid(); public string? name { get; set; } public string? key { get; set; } public string icon { get; set; } = "📚"; public string color { get; set; } = "#E8879E"; public List<Col> columns { get; set; } = new(); public List<Item> items { get; set; } = new(); }
public class AppState { public string lang { get; set; } = "es"; public string themeId { get; set; } = "rosa"; public bool darkMode { get; set; } public bool onboarded { get; set; } public List<Subject> subjects { get; set; } = new(); }

// El JSON usa los mismos nombres que la versión web/Android: los backups son intercambiables.
public static class Store
{
    public static AppState S = new();
    static Dictionary<string, Dictionary<string, string>> L = new();
    static readonly JsonSerializerOptions O = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    static string F => Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "progress.json");
    public static readonly (string id, string name, string hex)[] Themes = {
        ("rosa","Sakura","#E8879E"),("lavanda","Lavender","#A898D4"),("menta","Matcha","#5FAF8E"),("cielo","Ocean","#6BA3D6"),
        ("durazno","Peach","#EE9F6E"),("limon","Honey","#D0B23C"),("coral","Coral","#E37F7F"),("lila","Wisteria","#BC85D6") };
    public static readonly Dictionary<string, string> LangNames = new() { ["es"] = "Español", ["en"] = "English", ["hi"] = "हिन्दी", ["hinglish"] = "Hinglish", ["pt"] = "Português", ["fr"] = "Français", ["ta"] = "தமிழ்", ["te"] = "తెలుగు", ["bn"] = "বাংলা", ["mr"] = "मराठी", ["gu"] = "ગુજરાતી", ["ar"] = "العربية" };
    public static IEnumerable<string> Codes => L.Keys;
    public static string Uid() => Guid.NewGuid().ToString("N")[..10];
    public static void Init()
    {
        try { L = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "i18n.json")))!; } catch { }
        try { if (File.Exists(F)) S = JsonSerializer.Deserialize<AppState>(File.ReadAllText(F), O) ?? new(); } catch { }
    }
    public static void Save() { try { File.WriteAllText(F, Json()); } catch { } }
    public static string Json() => JsonSerializer.Serialize(S, O);
    public static bool Import(string t) { try { var d = JsonSerializer.Deserialize<AppState>(t, O); if (d == null) return false; d.onboarded = true; S = d; Save(); return true; } catch { return false; } }
    public static string T(string k) => L.GetValueOrDefault(S.lang)?.GetValueOrDefault(k) ?? L.GetValueOrDefault("en")?.GetValueOrDefault(k) ?? L.GetValueOrDefault("es")?.GetValueOrDefault(k) ?? k;
    public static string Name(Subject s) => s.name ?? T(s.key ?? "");
    public static string Name(Col c) => c.name ?? T(c.key ?? "");
    public static readonly string[] ColIcons = { "📖", "📝", "🧩", "✍️", "📘", "🗂️", "🔄", "📌" };
    public static List<Col> Cols() => new[] { "lecture", "dpp", "kpp", "assignment", "ncertReading", "pyqs", "revision" }.Select((k, i) => new Col { key = k, icon = ColIcons[i] }).ToList();
    public static List<Subject> IndiaPreset() => new[] { ("chemistry", "🧪"), ("physics", "⚛️"), ("botany", "🌿"), ("zoology", "🦋") }
        .Select((d, i) => new Subject { key = d.Item1, icon = d.Item2, color = Themes[i].hex, columns = Cols() }).ToList();
}
