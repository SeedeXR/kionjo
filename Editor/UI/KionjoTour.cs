using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// The guided first run: numbered steps in a panel at the bottom of the window, each one
    /// outlining the element it talks about and switching to its tab. Shown once on the first open,
    /// re-opened from Help > Guided tour.
    /// </summary>
    public class KionjoTour : VisualElement
    {
        public const string SeenKey = "SeedeXR.Kionjo.TourSeen";

        /// <summary>Element to outline (by name, first match wins), the tab it lives on, title, text.</summary>
        public readonly struct Step
        {
            public readonly string[] targets;
            public readonly KionjoWindow.Tab? tab;
            public readonly string title, text;

            public Step(string title, string text, KionjoWindow.Tab? tab, params string[] targets)
            {
                this.title = title;
                this.text = text;
                this.tab = tab;
                this.targets = targets;
            }
        }

        public static readonly Step[] Steps =
        {
            new("Create a storyboard",
                "A storyboard is your trailer on one page: the shots in order, how long each runs, what is said over it. Click Create storyboard (or New in the toolbar) and pick where the asset goes.",
                null, "create-storyboard", "new-storyboard"),
            new("Add a shot",
                "Frame the place in the Scene view first: a new shot takes its picture from the Scene view. Add card makes a black, text or image card instead.",
                KionjoWindow.Tab.Storyboard, "add-shot"),
            new("Fill in the shot",
                "Give it a name, a planned length, the voice line that plays over it and its subtitle. Every heading has a ? that explains its fields. The bar at the bottom shows the running length against your target.",
                KionjoWindow.Tab.Storyboard, "shot-inspector"),
            new("Its picture",
                "From Scene view or From camera retakes the picture, or drop a PNG or JPG on it. A render replaces it with its middle frame. Cut > Build animatic turns these pictures into a timed preview.",
                KionjoWindow.Tab.Storyboard, "picture-preview", "shot-inspector"),
            new("Shoot",
                "Enter Play mode with the headset on (or your camera rig). Stage sets the scene up for the shot, Space starts and stops a take. Add a Kionjo Recorder first if your rig has none.",
                KionjoWindow.Tab.Shoot, "shoot-record", "add-recorder", "enter-play-mode"),
            new("Cut",
                "Render missing shots replays each take frame locked and renders it. Build draft assembles the trailer with voice, subtitles and music in a numbered folder.",
                KionjoWindow.Tab.Cut, "cut-build"),
        };

        readonly KionjoWindow _window;
        readonly Label _count, _title, _text;
        readonly Button _back, _next;
        VisualElement _outlined;
        int _index;

        public int Index => _index;

        public KionjoTour(KionjoWindow window)
        {
            _window = window;
            name = "tour";
            AddToClassList("kionjo-tour");
            pickingMode = PickingMode.Ignore; // the window stays usable under the panel

            var card = new VisualElement { name = "tour-card" };
            card.AddToClassList("kionjo-tour__card");
            _count = new Label { name = "tour-count" };
            _count.AddToClassList("kionjo-tour__count");
            _title = new Label { name = "tour-title" };
            _title.AddToClassList("kionjo-tour__title");
            _text = new Label { name = "tour-text" };
            _text.AddToClassList("kionjo-tour__text");
            card.Add(_count);
            card.Add(_title);
            card.Add(_text);
            var row = new VisualElement();
            row.AddToClassList("kionjo-row");
            row.Add(new Button(Close) { name = "tour-close", text = "Close", tooltip = "Close the tour. Help > Guided tour opens it again." });
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            _back = new Button(() => Go(_index - 1)) { name = "tour-back", text = "Back", tooltip = "The previous step." };
            _next = new Button(() => Go(_index + 1)) { name = "tour-next", tooltip = "The next step." };
            row.Add(_back);
            row.Add(_next);
            card.Add(row);
            Add(card);
        }

        public void Go(int index)
        {
            if (index >= Steps.Length)
            {
                Close();
                return;
            }
            _index = Mathf.Clamp(index, 0, Steps.Length - 1);
            var step = Steps[_index];
            if (step.tab.HasValue && _window.Storyboard != null) _window.CurrentTab = step.tab.Value;
            _count.text = $"Step {_index + 1} of {Steps.Length}";
            _title.text = step.title;
            _text.text = step.text;
            _back.SetEnabled(_index > 0);
            _next.text = _index == Steps.Length - 1 ? "Finish" : "Next";
            // Outline once the tab switch has laid out, so a hidden element is never picked.
            schedule.Execute(() => Outline(step));
        }

        void Outline(Step step)
        {
            _outlined?.RemoveFromClassList("kionjo-tour-target");
            _outlined = null;
            var root = _window.rootVisualElement;
            foreach (var target in step.targets)
            {
                var e = root.Q(target);
                if (e == null || !Visible(e)) continue;
                e.AddToClassList("kionjo-tour-target");
                _outlined = e;
                return;
            }
        }

        static bool Visible(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.ClassListContains("kionjo-hidden") || p.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        public void Close()
        {
            _outlined?.RemoveFromClassList("kionjo-tour-target");
            _outlined = null;
            EditorPrefs.SetBool(SeenKey, true);
            RemoveFromHierarchy();
        }
    }
}
