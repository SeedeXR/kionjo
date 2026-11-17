using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Edits one journal entry. Every change is saved to entry.json at once.</summary>
    public class JournalEntryView : VisualElement
    {
        static readonly string[] Roles = { "hook", "before", "after", "broll", "still", "facecam" };
        static readonly string[] Statuses = { "Draft", "Ready", "Posted" };

        JournalEntry _e;
        KionjoBrandKit _kit;
        public event Action Changed;

        public void Bind(JournalEntry e, KionjoBrandKit kit)
        {
            _e = e;
            _kit = kit;
            Clear();
            if (e == null) return;
            if (string.IsNullOrEmpty(e.owner) && kit != null) e.owner = kit.kitName;
            Add(KionjoHelp.Section("Entry"));
            Add(Text("Title", e.title, v => e.title = v));
            Add(Text("Project", e.project, v => e.project = v));
            Add(Text("Owner", e.owner, v => e.owner = v, "Brand kit name of the person or studio posting it."));
            var stories = (kit != null ? kit.storyTypes : new List<string> { "Origin", "Villain", "Magic", "Prophecy", "Alchemy" }).ToList();
            if (!string.IsNullOrEmpty(e.story) && !stories.Contains(e.story)) stories.Add(e.story);
            Add(Choice("Story", stories, e.story, v => e.story = v));
            Add(Choice("Stage", KionjoJournal.Stages.ToList(), e.stage, v => e.stage = v));
            Add(Choice("Status", Statuses.ToList(), e.status, v => e.status = v));
            Add(KionjoHelp.Section("Beats"));
            Add(Text("Making", e.making, v => e.making = v, "What you are making.", true));
            Add(Text("Reference", e.reference, v => e.reference = v, "What it is based on.", true));
            Add(Text("Goal", e.goal, v => e.goal = v, "What this step wanted.", true));
            Add(Text("Achieved", e.achieved, v => e.achieved = v, "What works now.", true));
            Add(Text("Hurdle", e.hurdle, v => e.hurdle = v, "What got in the way.", true));
            Add(Text("Trick", e.trick, v => e.trick = v, "How you got past it.", true));
            Add(KionjoHelp.Section("Decisions"));
            Add(StringList("decisions", e.decisions, "Add decision"));
            Add(KionjoHelp.Section("Hook options"));
            Add(StringList("hook-options", e.hookOptions, "Add hook option"));
            Add(KionjoHelp.Section("Media"));
            var media = new VisualElement { name = "entry-media" };
            for (int i = 0; i < e.media.Count; i++) media.Add(MediaRow(e.media[i]));
            Add(media);
            Add(new Button(() =>
            {
                var path = EditorUtility.OpenFilePanel("Add media", "", "mp4,mov,mkv,webm,png,jpg,jpeg");
                if (string.IsNullOrEmpty(path)) return;
                e.media.Add(new JournalMedia { path = path, role = e.media.Count == 0 ? "hook" : "broll" });
                Save(rebind: true);
            }) { name = "add-media", text = "Add file", tooltip = "Reference a clip or picture by its absolute path (nothing is copied)." });
        }

        void Save(bool rebind = false)
        {
            KionjoJournal.SaveEdited(_e);
            if (rebind) Bind(_e, _kit);
            Changed?.Invoke();
        }

        TextField Text(string label, string value, Action<string> set, string tip = null, bool multi = false)
        {
            var f = new TextField(label) { value = value ?? "", multiline = multi, tooltip = tip ?? "", isDelayed = true };
            f.style.whiteSpace = WhiteSpace.Normal;
            f.RegisterValueChangedCallback(ev =>
            {
                set(ev.newValue);
                Save();
            });
            return f;
        }

        DropdownField Choice(string label, List<string> choices, string value, Action<string> set)
        {
            var f = new DropdownField(label, choices, Math.Max(0, choices.IndexOf(value ?? "")));
            if (string.IsNullOrEmpty(value)) f.SetValueWithoutNotify("");
            f.RegisterValueChangedCallback(ev =>
            {
                set(ev.newValue);
                Save();
            });
            return f;
        }

        VisualElement StringList(string name, List<string> list, string addText)
        {
            var box = new VisualElement { name = name };
            for (int i = 0; i < list.Count; i++)
            {
                int at = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var f = new TextField { value = list[i] ?? "", isDelayed = true, style = { flexGrow = 1 } };
                f.RegisterValueChangedCallback(ev =>
                {
                    list[at] = ev.newValue;
                    Save();
                });
                row.Add(f);
                row.Add(new Button(() =>
                {
                    list.RemoveAt(at);
                    Save(rebind: true);
                }) { text = "x", tooltip = "Remove" });
                box.Add(row);
            }
            box.Add(new Button(() =>
            {
                list.Add("");
                Save(rebind: true);
            }) { text = addText });
            return box;
        }

        VisualElement MediaRow(JournalMedia m)
        {
            bool missing = string.IsNullOrEmpty(m.path) || !File.Exists(m.path);
            var box = new VisualElement { style = { marginBottom = 6 } };
            var top = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var path = new TextField { value = m.path ?? "", isDelayed = true, style = { flexGrow = 1 }, tooltip = missing ? "This file is missing: relink it." : m.path };
            path.EnableInClassList("kionjo-bad", missing);
            path.RegisterValueChangedCallback(ev =>
            {
                m.path = ev.newValue;
                Save(rebind: true);
            });
            top.Add(path);
            top.Add(new Button(() =>
            {
                var picked = EditorUtility.OpenFilePanel(missing ? "Relink media" : "Replace media", string.IsNullOrEmpty(m.path) ? "" : Path.GetDirectoryName(m.path) ?? "", "");
                if (string.IsNullOrEmpty(picked)) return;
                m.path = picked;
                Save(rebind: true);
            }) { text = missing ? "Relink" : "...", tooltip = missing ? "Point this item at the file's new place." : "Pick another file." });
            top.Add(new Button(() =>
            {
                _e.media.Remove(m);
                Save(rebind: true);
            }) { text = "x", tooltip = "Remove from the entry (the file stays on disk)." });
            box.Add(top);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var role = new DropdownField(Roles.ToList(), Math.Max(0, Array.IndexOf(Roles, m.role)));
            role.RegisterValueChangedCallback(ev =>
            {
                m.role = ev.newValue;
                Save();
            });
            row.Add(role);
            var inF = new FloatField("In") { value = m.inPoint };
            inF.RegisterValueChangedCallback(ev =>
            {
                m.inPoint = ev.newValue;
                Save();
            });
            var outF = new FloatField("Out") { value = m.outPoint, tooltip = "Out at or before In means to the end of the file." };
            outF.RegisterValueChangedCallback(ev =>
            {
                m.outPoint = ev.newValue;
                Save();
            });
            var notOwn = new Toggle("Not mine") { value = m.notOwn, tooltip = "Footage someone else made: it needs your text or voice on top." };
            notOwn.RegisterValueChangedCallback(ev =>
            {
                m.notOwn = ev.newValue;
                Save();
            });
            foreach (var f in new VisualElement[] { inF, outF, notOwn }) row.Add(f);
            box.Add(row);
            return box;
        }
    }
}
