using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>The trailer at a glance: one segment per shot, as wide as its planned length, and the running total against the target.</summary>
    public class TimelineBar : VisualElement
    {
        readonly VisualElement _bar;
        readonly Label _total;
        public event Action<int> ShotClicked;

        public TimelineBar()
        {
            AddToClassList("kionjo-timeline");
            _bar = new VisualElement { name = "timeline-segments" };
            _bar.AddToClassList("kionjo-timeline__bar");
            Add(_bar);
            _total = new Label { name = "timeline-total" };
            _total.AddToClassList("kionjo-timeline__total");
            Add(_total);
        }

        public string TotalText => _total.text;

        public static string FormatTotal(float total, float target) => $"{total:0.0} s / target {target:0} s";

        public void Refresh(KionjoStoryboard sb, int selected)
        {
            _bar.Clear();
            if (sb == null)
            {
                _total.text = "";
                return;
            }
            float total = 0f;
            for (int i = 0; i < sb.shots.Count; i++)
            {
                var shot = sb.shots[i];
                float seconds = Mathf.Max(0f, shot.PlannedSeconds());
                total += seconds;
                var seg = new VisualElement { tooltip = $"{shot.code}  {shot.name}: {seconds:0.#} s" };
                seg.AddToClassList("kionjo-timeline__segment");
                seg.EnableInClassList("kionjo-timeline__segment--card", shot.IsCard);
                seg.EnableInClassList("kionjo-timeline__segment--selected", i == selected);
                seg.style.flexGrow = Mathf.Max(0.1f, seconds);
                seg.style.flexBasis = 0;
                var label = new Label(shot.code) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("kionjo-timeline__label");
                seg.Add(label);
                int index = i;
                seg.RegisterCallback<ClickEvent>(_ => ShotClicked?.Invoke(index));
                _bar.Add(seg);
            }
            _total.text = FormatTotal(total, sb.targetSeconds);
            bool over = total > sb.targetSeconds + 0.05f;
            _total.EnableInClassList("kionjo-timeline__total--over", over);
            _total.tooltip = over ? $"{total - sb.targetSeconds:0.0} s over the target length" : "Planned length of the whole trailer";
        }
    }
}
