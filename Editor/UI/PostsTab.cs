using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The Posts tab: the journal (entries, story balance) on the left, the selected entry and its
    /// post builder on the right. Works with no storyboard open.
    /// </summary>
    public class PostsTab : VisualElement
    {
        readonly ListView _list;
        readonly Label _folder, _balance, _warnings;
        readonly ObjectField _kit;
        readonly TextField _newTitle;
        readonly JournalEntryView _entryView;
        readonly PostBuildView _buildView;
        readonly VisualElement _detail;
        List<JournalEntry> _entries = new();

        public JournalEntry Selected => _list.selectedIndex >= 0 && _list.selectedIndex < _entries.Count ? _entries[_list.selectedIndex] : null;
        public IReadOnlyList<JournalEntry> Entries => _entries;

        public PostsTab(KionjoWindow w)
        {
            style.flexGrow = 1;
            var top = new VisualElement { name = "posts-top", style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4, flexWrap = Wrap.Wrap } };
            _folder = new Label { name = "journal-folder", style = { flexShrink = 1, marginRight = 4 } };
            top.Add(_folder);
            top.Add(new Button(KionjoPreferences.Open) { name = "journal-change", text = "Change", tooltip = "Pick the journal folder in Preferences > Kionjo." });
            _kit = new ObjectField("Brand kit") { name = "posts-kit", objectType = typeof(KionjoBrandKit), allowSceneObjects = false, tooltip = "The owner's look and words: fonts, colours, caption lines, story types." };
            _kit.style.minWidth = 260;
            _kit.style.marginLeft = 12;
            _kit.RegisterValueChangedCallback(e =>
            {
                KionjoPostsSettings.instance.ActiveKit = e.newValue as KionjoBrandKit;
                KionjoPostsSettings.instance.SaveNow();
                Refresh();
            });
            top.Add(_kit);
            _newTitle = new TextField { name = "new-entry-title", tooltip = "Working title of the new entry, for example \"The ghost never turns round\"." };
            _newTitle.style.minWidth = 160;
            _newTitle.style.marginLeft = 12;
            top.Add(_newTitle);
            top.Add(new Button(NewEntry) { name = "new-entry", text = "New entry", tooltip = "Create a journal entry for a step of the work." });
            Add(top);

            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal) { name = "posts-split" };
            split.style.flexGrow = 1;
            var left = new VisualElement { style = { flexGrow = 1 } };
            _list = new ListView
            {
                name = "journal-list",
                fixedItemHeight = 40,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6 } },
                bindItem = (el, i) =>
                {
                    var e = _entries[i];
                    ((Label)el).text = $"{e.title}\n{e.project} · {(string.IsNullOrEmpty(e.story) ? "no story" : e.story)} · {e.status}";
                    el.EnableInClassList("kionjo-bad", KionjoJournal.Missing(e).Any());
                },
            };
            _list.style.flexGrow = 1;
            _list.selectionChanged += _ => ShowSelected();
            left.Add(_list);
            _balance = new Label { name = "story-balance", tooltip = "How the last 12 entries spread across the kit's story types.", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingTop = 4, paddingBottom = 4 } };
            left.Add(_balance);
            _warnings = new Label { name = "journal-warnings", style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6 } };
            _warnings.AddToClassList("kionjo-bad");
            left.Add(_warnings);
            split.Add(left);

            var scroll = new ScrollView { name = "posts-detail-scroll" };
            _detail = new VisualElement { style = { paddingLeft = 8, paddingRight = 8 } };
            _entryView = new JournalEntryView { name = "journal-entry" };
            _entryView.Changed += () =>
            {
                _list.RefreshItems();
                _buildView.Bind(Selected, Kit);
            };
            _buildView = new PostBuildView { name = "post-build-view" };
            _detail.Add(_entryView);
            _detail.Add(_buildView);
            scroll.Add(_detail);
            split.Add(scroll);
            Add(split);
            _detail.style.display = DisplayStyle.None; // filled by Refresh when the tab is shown
        }

        static KionjoBrandKit Kit => KionjoPostsSettings.instance.ActiveKit;

        public void Refresh()
        {
            var keep = Selected?.Folder;
            var warnings = new List<string>();
            _entries = KionjoJournal.LoadAll(KionjoJournal.Folder, warnings);
            _folder.text = $"Journal: {KionjoJournal.Folder}";
            _kit.SetValueWithoutNotify(Kit);
            _list.itemsSource = _entries;
            _list.Rebuild();
            int at = _entries.FindIndex(e => e.Folder == keep);
            if (at < 0 && _entries.Count > 0) at = 0;
            if (at >= 0) _list.SetSelectionWithoutNotify(new[] { at });
            var types = Kit != null ? Kit.storyTypes : CreateDefaultTypes();
            _balance.text = StoryBalance(_entries, types);
            _warnings.text = string.Join("\n", warnings);
            ShowSelected();
        }

        static List<string> CreateDefaultTypes() => new() { "Origin", "Villain", "Magic", "Prophecy", "Alchemy" };

        void ShowSelected()
        {
            var e = Selected;
            _detail.style.display = e == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (e == null) return;
            _entryView.Bind(e, Kit);
            _buildView.Bind(e, Kit);
        }

        void NewEntry()
        {
            var title = string.IsNullOrWhiteSpace(_newTitle.value) ? "New entry" : _newTitle.value.Trim();
            var e = KionjoJournal.Create(KionjoJournal.Folder, title, UnityEngine.Application.productName, System.DateTime.Now);
            if (Kit != null)
            {
                e.owner = Kit.kitName;
                KionjoJournal.Save(e);
            }
            _newTitle.SetValueWithoutNotify("");
            Refresh();
            _list.SetSelection(_entries.FindIndex(x => x.Folder == e.Folder));
        }

        /// <summary>"Magic 5 · Origin 1 · Villain 0": story types by count over the 12 newest entries (newest first in the list).</summary>
        public static string StoryBalance(IList<JournalEntry> newestFirst, IList<string> storyTypes)
        {
            var recent = newestFirst.Take(12).ToList();
            return string.Join(" · ", storyTypes
                .Select((t, i) => (t, i, n: recent.Count(e => string.Equals(e.story, t, System.StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(x => x.n).ThenBy(x => x.i)
                .Select(x => $"{x.t} {x.n}"));
        }
    }
}
