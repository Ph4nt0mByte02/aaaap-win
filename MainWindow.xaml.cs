using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;
using static MyProgress.Store;
namespace MyProgress;

public sealed partial class MainWindow : Window
{
    Subject? cur; bool edit;
    static readonly string[] Emojis = "🧪 ⚛️ 🌿 🦋 📐 🧬 💻 📚 🌍 🔬 🎨 📊 🩺 ⚗️ 🧠 🔭".Split(' ');

    public MainWindow()
    {
        InitializeComponent(); Title = "My Progress"; SystemBackdrop = new MicaBackdrop();
        Nav.SelectedItem = Nav.MenuItems[0];
        Nav.Loaded += async (_, _) => { if (!S.onboarded) await Onboard(); Refresh(); };
    }

    // ---------- tema ----------
    static Color H(string h) => Color.FromArgb(255, Convert.ToByte(h.Substring(1, 2), 16), Convert.ToByte(h.Substring(3, 2), 16), Convert.ToByte(h.Substring(5, 2), 16));
    static Color Mix(Color a, Color b, double t) => Color.FromArgb(255, (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    static SolidColorBrush B(Color c) => new SolidColorBrush(c);
    static Color P => H((Themes.FirstOrDefault(t => t.id == S.themeId).hex) ?? Themes[0].hex);
    static Color Bg => S.darkMode ? Mix(P, Colors.Black, .88) : Mix(P, Colors.White, .94);
    static Color Sf => S.darkMode ? Mix(P, Colors.Black, .78) : Colors.White;
    static Color Bd => S.darkMode ? Mix(P, Colors.Black, .6) : Mix(P, Colors.White, .82);

    // ---------- helpers UI ----------
    static TextBlock Tx(string s, double sz = 14, bool bold = false) { var t = new TextBlock { Text = s, FontSize = sz, TextWrapping = TextWrapping.Wrap }; if (bold) t.FontWeight = FontWeights.Bold; return t; }
    static Button Btn(string s, Action a) { var b = new Button { Content = s, HorizontalAlignment = HorizontalAlignment.Stretch }; b.Click += (_, _) => a(); return b; }
    static Border Card(UIElement c) => new Border { Child = c, Padding = new Thickness(16), CornerRadius = new CornerRadius(20), Background = B(Sf), BorderBrush = B(Bd), BorderThickness = new Thickness(1) };
    static StackPanel Page() => new StackPanel { Margin = new Thickness(24), MaxWidth = 900, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
    static Border Tile(string emoji, Color c, double size) => new Border { Width = size, Height = size, CornerRadius = new CornerRadius(16), Background = B(Mix(c, Sf, .8)), Child = new TextBlock { Text = emoji, FontSize = size / 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    static UIElement Ring(int pct, Color c, double size, double fs)
    {
        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new ProgressRing { IsIndeterminate = false, Value = pct, Maximum = 100, Width = size, Height = size, Foreground = B(c), Background = B(Bd) });
        g.Children.Add(new TextBlock { Text = pct + "%", FontSize = fs, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        return g;
    }
    static (int dn, int tot, int pct) Prog(Subject s) { int t = s.items.Count, d = s.items.Count(i => i.done); return (d, t, t == 0 ? 0 : (int)Math.Round(100.0 * d / t)); }
    ElementTheme Th => S.darkMode ? ElementTheme.Dark : ElementTheme.Light;

    async Task<bool> Ask(string title, UIElement? body, string ok)
    {
        var d = new ContentDialog { XamlRoot = Nav.XamlRoot, RequestedTheme = Th, FlowDirection = Nav.FlowDirection, Title = title, Content = body, PrimaryButtonText = ok, CloseButtonText = T("cancel"), DefaultButton = ContentDialogButton.Primary };
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }
    async void Prompt(string title, string init, Action<string> done, bool allowEmpty = false)
    {
        var tb = new TextBox { Text = init, MaxLength = 40 };
        if (await Ask(title, tb, T("save")) && (allowEmpty || tb.Text.Trim() != "")) done(tb.Text.Trim());
    }
    async void Confirm(string msg, Action yes) { if (await Ask(T("confirm"), Tx(msg), T("confirm"))) yes(); }
    async void Note(string m) => await new ContentDialog { XamlRoot = Nav.XamlRoot, RequestedTheme = Th, FlowDirection = Nav.FlowDirection, Content = Tx(m), CloseButtonText = "OK" }.ShowAsync();
    void Commit() { Save(); Refresh(); }

    async Task Onboard()
    {
        var codes = Codes.ToList();
        var cb = new ComboBox { ItemsSource = codes.Select(k => LangNames.GetValueOrDefault(k, k)).ToList(), SelectedIndex = Math.Max(0, codes.IndexOf(S.lang)), HorizontalAlignment = HorizontalAlignment.Stretch };
        cb.SelectionChanged += (_, _) => { if (cb.SelectedIndex >= 0) S.lang = codes[cb.SelectedIndex]; };
        var sp = new StackPanel { Spacing = 10 }; sp.Children.Add(Tx(T("onbLang"))); sp.Children.Add(cb);
        await new ContentDialog { XamlRoot = Nav.XamlRoot, RequestedTheme = Th, FlowDirection = Nav.FlowDirection, Title = T("onbTitle"), Content = sp, PrimaryButtonText = T("onbNext") }.ShowAsync();
        Refresh(); // aplica idioma y RTL
        var d = new ContentDialog { XamlRoot = Nav.XamlRoot, RequestedTheme = Th, FlowDirection = Nav.FlowDirection, Title = T("onbText"), PrimaryButtonText = T("onbIndia"), SecondaryButtonText = T("onbCustom") };
        var r = await d.ShowAsync();
        S.subjects = r == ContentDialogResult.Primary ? IndiaPreset() : new(); S.onboarded = true; Save();
    }

    // ---------- navegación ----------
    void OnNav(NavigationView s, NavigationViewSelectionChangedEventArgs a) { cur = null; edit = false; Refresh(); }
    void Refresh()
    {
        Nav.RequestedTheme = Th; Nav.Background = B(Bg); Nav.FlowDirection = S.lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var it = Nav.MenuItems.Cast<NavigationViewItem>().ToList();
        it[0].Content = T("navChart"); it[1].Content = T("navStats"); it[2].Content = T("navSettings");
        var tag = (Nav.SelectedItem as NavigationViewItem)?.Tag as string ?? "chart";
        Body.Content = tag == "stats" ? Stats() : tag == "settings" ? Settings() : cur != null ? Detail(cur) : Subjects();
    }

    // ---------- materias ----------
    UIElement Subjects()
    {
        var p = Page(); p.Children.Add(Tx(T("appName"), 28, true)); p.Children.Add(Tx(T("subjectsTitle"), 13));
        if (S.subjects.Count == 0) p.Children.Add(Card(Tx(T("noSubjects") + "\n" + T("noSubjectsHint"))));
        foreach (var s in S.subjects)
        {
            var c = H(s.color); var pr = Prog(s);
            var g = new Grid { ColumnSpacing = 14 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; info.Children.Add(Tx(Name(s), 16, true)); info.Children.Add(Tx($"{pr.dn} {T("completedOf")} {pr.tot}", 12));
            var ring = Ring(pr.pct, c, 48, 11);
            g.Children.Add(Tile(s.icon, c, 52)); g.Children.Add(info); g.Children.Add(ring); Grid.SetColumn(info, 1); Grid.SetColumn(ring, 2);
            var card = Card(g); var sub = s; card.Tapped += (_, _) => { cur = sub; Refresh(); };
            p.Children.Add(card);
        }
        p.Children.Add(Btn("➕ " + T("addSubject"), AddSubject)); return p;
    }
    async void AddSubject()
    {
        var tb = new TextBox { PlaceholderText = T("subjectNamePlaceholder"), MaxLength = 30 };
        var cb = new ComboBox { ItemsSource = Emojis, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 0) };
        var sp = new StackPanel(); sp.Children.Add(tb); sp.Children.Add(cb);
        if (await Ask(T("newSubject"), sp, T("save")) && tb.Text.Trim() != "")
        { S.subjects.Add(new Subject { name = tb.Text.Trim(), icon = (string)cb.SelectedItem, color = Themes[S.subjects.Count % 8].hex, columns = Cols() }); Commit(); }
    }

    // ---------- detalle ----------
    UIElement Detail(Subject s)
    {
        var p = Page(); var c = H(s.color); var pr = Prog(s);
        var head = new Grid { ColumnSpacing = 14 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };
        var nm = Tx(Name(s), 22, true); if (edit) nm.Tapped += (_, _) => Prompt(T("renameSubject"), Name(s), v => { s.name = v; s.key = null; Commit(); });
        var bar = new ProgressBar { Value = pr.pct, Maximum = 100, Foreground = B(c) }; mid.Children.Add(nm); mid.Children.Add(bar);
        var tg = new ToggleButton { Content = "✎", IsChecked = edit }; tg.Click += (_, _) => { edit = !edit; Refresh(); };
        var tile = Tile(s.icon, c, 58); head.Children.Add(tile); head.Children.Add(mid); head.Children.Add(tg); Grid.SetColumn(mid, 1); Grid.SetColumn(tg, 2);
        p.Children.Add(head);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var col in s.columns.ToList())
        {
            var its = s.items.Where(i => i.columnId == col.id).ToList();
            var st = new StackPanel { Spacing = 4, Width = 200 };
            var h = new Grid(); h.ColumnDefinitions.Add(new ColumnDefinition()); h.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = Tx($"{col.icon} {Name(col)}", 14, true); var cnt = Tx($"{its.Count(i => i.done)}/{its.Count}", 11);
            if (edit) title.Tapped += (_, _) => Prompt(T("renameColumn"), Name(col), v => { col.name = v; col.key = null; Commit(); });
            UIElement right = cnt; if (edit) right = Btn("✕", () => Confirm(T("confirmDeleteColumn"), () => { s.columns.Remove(col); s.items.RemoveAll(i => i.columnId == col.id); Commit(); }));
            h.Children.Add(title); h.Children.Add(right); Grid.SetColumn((FrameworkElement)right, 1); st.Children.Add(h);
            foreach (var it in its)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                var cb = new CheckBox { IsChecked = it.done, Content = $"{Name(col)} {it.number}" + (it.chapter != "" ? "\n" + it.chapter : ""), MinWidth = 0 };
                cb.Click += (_, _) => { it.done = cb.IsChecked == true; it.doneDate = it.done ? DateTime.Now.ToString("yyyy-MM-dd") : null; Save(); var q = Prog(s); bar.Value = q.pct; cnt.Text = $"{its.Count(i => i.done)}/{its.Count}"; };
                line.Children.Add(cb);
                if (edit) { line.Children.Add(Btn("✎", () => Prompt(T("editChapter"), it.chapter, v => { it.chapter = v; Commit(); }, true))); line.Children.Add(Btn("✕", () => Confirm(T("confirmDeleteItem"), () => { s.items.Remove(it); Commit(); }))); }
                st.Children.Add(line);
            }
            st.Children.Add(Btn(T("addItem"), () => { s.items.Add(new Item { columnId = col.id, number = (its.Count + 1).ToString("00") }); Commit(); }));
            row.Children.Add(Card(st));
        }
        row.Children.Add(Btn("➕", () => Prompt(T("addColumn"), "", v => { s.columns.Add(new Col { name = v, icon = ColIcons[s.columns.Count % 8] }); Commit(); })));
        p.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = row });
        if (edit) p.Children.Add(Btn("🗑️ " + T("delete"), () => Confirm(T("confirmDeleteSubject"), () => { S.subjects.Remove(s); cur = null; edit = false; Commit(); })));
        p.Children.Add(Btn("← " + T("back"), () => { cur = null; edit = false; Refresh(); })); return p;
    }

    static string WD(DateTime d) { var w = T("weekDays").Split(','); return w[(int)d.DayOfWeek]; }

    // ---------- progreso ----------
    UIElement Stats()
    {
        var p = Page(); var all = S.subjects.SelectMany(x => x.items).ToList(); int tot = all.Count, dn = all.Count(i => i.done);
        p.Children.Add(Tx(T("overallProgress"), 28, true));
        var r = Ring(tot == 0 ? 0 : (int)Math.Round(100.0 * dn / tot), P, 150, 30); ((FrameworkElement)r).HorizontalAlignment = HorizontalAlignment.Center; p.Children.Add(Card(r));
        var ds = all.Where(i => i.done && i.doneDate != null).Select(i => i.doneDate!).ToList(); int streak = 0; var d0 = DateTime.Now.Date;
        while (ds.Contains(d0.ToString("yyyy-MM-dd"))) { streak++; d0 = d0.AddDays(-1); }
        p.Children.Add(Card(Tx($"{dn}/{tot} {T("totalItems")}    🔥 {streak} {T("streakDays")}", 16, true)));
        p.Children.Add(Tx(T("last7"), 19, true));
        var days = Enumerable.Range(0, 7).Select(i => DateTime.Now.Date.AddDays(i - 6)).ToList(); var cnts = days.Select(d => ds.Count(x => x == d.ToString("yyyy-MM-dd"))).ToList(); int mx = Math.Max(1, cnts.Max());
        var week = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < 7; i++)
        {
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 4, Width = 36 };
            col.Children.Add(new Border { Height = 4 + 80.0 * cnts[i] / mx, CornerRadius = new CornerRadius(6), Background = B(P) }); col.Children.Add(new TextBlock { Text = WD(days[i]), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
            week.Children.Add(col);
        }
        p.Children.Add(Card(week)); p.Children.Add(Tx(T("bySubject"), 19, true));
        var list = new StackPanel { Spacing = 12 };
        foreach (var s in S.subjects) { var q = Prog(s); var g = new Grid { ColumnSpacing = 10 }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            var l = Tx($"{s.icon} {Name(s)}", 12, true); var b = new ProgressBar { Value = q.pct, Maximum = 100, Foreground = B(H(s.color)) }; var pc = Tx(q.pct + "%", 11);
            g.Children.Add(l); g.Children.Add(b); g.Children.Add(pc); Grid.SetColumn(b, 1); Grid.SetColumn(pc, 2); list.Children.Add(g); }
        p.Children.Add(Card(list)); return p;
    }

    // ---------- ajustes ----------
    UIElement Settings()
    {
        var p = Page(); p.Children.Add(Tx(T("navSettings"), 28, true)); p.Children.Add(Tx(T("appearance"), 12, true));
        var ts = new ToggleSwitch { IsOn = S.darkMode, Header = "🌙 " + T("darkMode") }; ts.Toggled += (_, _) => { S.darkMode = ts.IsOn; Commit(); }; p.Children.Add(Card(ts));
        var codes = Codes.ToList(); var lc = new ComboBox { Header = "🌐 " + T("language"), ItemsSource = codes.Select(k => LangNames.GetValueOrDefault(k, k)).ToList(), SelectedIndex = Math.Max(0, codes.IndexOf(S.lang)) };
        lc.SelectionChanged += (_, _) => { if (lc.SelectedIndex >= 0 && codes[lc.SelectedIndex] != S.lang) { S.lang = codes[lc.SelectedIndex]; Commit(); } }; p.Children.Add(Card(lc));
        p.Children.Add(Tx(T("theme"), 12, true));
        var sw = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var th in Themes) { var id = th.id; var b = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = B(H(th.hex)), BorderThickness = new Thickness(S.themeId == id ? 3 : 0), BorderBrush = new SolidColorBrush(S.darkMode ? Colors.White : Colors.Black) }; b.Tapped += (_, _) => { S.themeId = id; Commit(); }; sw.Children.Add(b); }
        p.Children.Add(Card(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = sw }));
        p.Children.Add(Tx(T("data"), 12, true));
        p.Children.Add(Btn("📤 " + T("exportData"), Export)); p.Children.Add(Btn("📥 " + T("importData"), Import));
        p.Children.Add(Btn("🗑️ " + T("clearData"), () => Confirm(T("confirmClear"), () => { S.subjects = IndiaPreset(); Commit(); }))); return p;
    }
    async void Export()
    {
        var pk = new FileSavePicker { SuggestedFileName = "mi-progreso-backup" }; pk.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        WinRT.Interop.InitializeWithWindow.Initialize(pk, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var f = await pk.PickSaveFileAsync(); if (f == null) return;
        await Windows.Storage.FileIO.WriteTextAsync(f, Json()); Note(T("exportSuccess"));
    }
    async void Import()
    {
        var pk = new FileOpenPicker(); pk.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(pk, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var f = await pk.PickSingleFileAsync(); if (f == null) return;
        if (MyProgress.Store.Import(await Windows.Storage.FileIO.ReadTextAsync(f))) { cur = null; Refresh(); Note(T("importSuccess")); } else Note(T("importError"));
    }
}
