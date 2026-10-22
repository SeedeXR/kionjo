using System.IO;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>One row of the shot list: thumbnail, code and name, type and location tags, length, state.</summary>
    public static class ShotCard
    {
        public static VisualElement Make()
        {
            var card = new VisualElement();
            card.AddToClassList("kionjo-card");

            var thumbBox = new VisualElement();
            thumbBox.AddToClassList("kionjo-card__thumb");
            var thumb = new Image { name = "thumb", scaleMode = UnityEngine.ScaleMode.ScaleAndCrop };
            thumb.style.flexGrow = 1;
            thumbBox.Add(thumb);
            var thumbLabel = new Label { name = "thumb-label", pickingMode = PickingMode.Ignore };
            thumbLabel.AddToClassList("kionjo-card__thumb-label");
            thumbBox.Add(thumbLabel);
            card.Add(thumbBox);

            var main = new VisualElement();
            main.AddToClassList("kionjo-card__main");
            var title = new VisualElement();
            title.AddToClassList("kionjo-card__title");
            var code = new Label { name = "code" };
            code.AddToClassList("kionjo-card__code");
            var nameLabel = new Label { name = "name" };
            nameLabel.AddToClassList("kionjo-card__name");
            title.Add(code);
            title.Add(nameLabel);
            main.Add(title);
            var tags = new VisualElement();
            tags.AddToClassList("kionjo-card__tags");
            foreach (var n in new[] { "type-tag", "location-tag", "beat-tag" })
            {
                var t = new Label { name = n };
                t.AddToClassList("kionjo-tag");
                tags.Add(t);
            }
            main.Add(tags);
            card.Add(main);

            var side = new VisualElement();
            side.AddToClassList("kionjo-card__side");
            var length = new Label { name = "length" };
            length.AddToClassList("kionjo-card__length");
            var state = new Label { name = "state" };
            state.AddToClassList("kionjo-card__state");
            side.Add(length);
            side.Add(state);
            card.Add(side);
            return card;
        }

        public static void Bind(VisualElement card, KionjoStoryboard sb, KionjoShot shot)
        {
            var tex = KionjoThumbnails.Get(shot.thumbnailPath);
            card.Q<Image>("thumb").image = tex;
            card.Q<Label>("thumb-label").text = tex != null ? "" : shot.IsCard ? CardLabel(shot) : "no image";
            card.Q<Label>("code").text = shot.code;
            card.Q<Label>("name").text = string.IsNullOrWhiteSpace(shot.name) ? "Untitled" : shot.name;
            SetTag(card.Q<Label>("type-tag"), shot.IsCard ? "card" : TypeLabel(shot.type));
            SetTag(card.Q<Label>("location-tag"), shot.location);
            SetTag(card.Q<Label>("beat-tag"), shot.beat);
            card.Q<Label>("length").text = $"{shot.PlannedSeconds():0.#} s";
            var state = card.Q<Label>("state");
            state.text = State(sb, shot, out bool ready);
            state.EnableInClassList("kionjo-card__state--ready", ready);
            card.tooltip = string.IsNullOrWhiteSpace(shot.notes) ? "" : shot.notes;
        }

        static void SetTag(Label tag, string text)
        {
            tag.text = text ?? "";
            tag.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static string CardLabel(KionjoShot shot) => shot.cardKind switch
        {
            KionjoCardKind.Text => shot.cardLines.Count > 0 ? shot.cardLines[0] : "text",
            KionjoCardKind.Image => "image",
            _ => "black",
        };

        static string TypeLabel(KionjoShotType t) => t switch
        {
            KionjoShotType.LiveCamera => "live camera",
            KionjoShotType.Still => "still",
            _ => "performance",
        };

        /// <summary>What the shot has on disk, in a few words.</summary>
        public static string State(KionjoStoryboard sb, KionjoShot shot, out bool ready)
        {
            ready = false;
            if (shot.IsCard)
            {
                ready = true;
                return $"{shot.cardKind.ToString().ToLowerInvariant()} card";
            }
            if (shot.outputMode == KionjoOutputMode.OrganiseOnly) return "organise only";
            if (shot.outputMode == KionjoOutputMode.DeviceCapture)
            {
                ready = !string.IsNullOrEmpty(shot.deviceCapturePath) && File.Exists(shot.deviceCapturePath);
                return ready ? "capture ready" : "no capture";
            }
            if (shot.type == KionjoShotType.Still)
            {
                ready = !string.IsNullOrEmpty(shot.thumbnailPath) && File.Exists(shot.thumbnailPath);
                return ready ? "still ready" : "no still";
            }
            if (KionjoPaths.ExistingRender(sb, shot) != null)
            {
                ready = true;
                return "rendered";
            }
            if (shot.type == KionjoShotType.LiveCamera) return "not rendered";
            var folder = KionjoPaths.ShotTakes(sb, shot);
            int takes = 0;
            if (Directory.Exists(folder))
                foreach (var d in Directory.GetDirectories(folder))
                    if (KionjoTakePaths.TryParseTakeNumber(Path.GetFileName(d), out _)) takes++;
            return takes == 0 ? "no takes" : takes == 1 ? "1 take" : $"{takes} takes";
        }
    }
}
