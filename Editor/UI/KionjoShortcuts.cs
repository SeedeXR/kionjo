using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Keyboard shortcuts while the Kionjo window has focus (rebindable in Edit > Shortcuts).</summary>
    static class KionjoShortcuts
    {
        static KionjoWindow Window(ShortcutArguments a) => a.context as KionjoWindow;

        /// <summary>True while a text field has the keyboard, so typing a space never starts a take.</summary>
        static bool Typing(KionjoWindow w)
        {
            var focused = w.rootVisualElement.focusController?.focusedElement as VisualElement;
            for (var e = focused; e != null; e = e.parent)
                if (e is TextField) return true;
            return false;
        }

        [Shortcut("Kionjo/Toggle Take", typeof(KionjoWindow), KeyCode.Space)]
        static void ToggleTake(ShortcutArguments a)
        {
            var w = Window(a);
            if (w == null || Typing(w) || w.CurrentTab != KionjoWindow.Tab.Shoot) return;
            w.rootVisualElement.Q<ShootTab>()?.Controller?.ToggleRecord();
        }

        [Shortcut("Kionjo/Done", typeof(KionjoWindow), KeyCode.Return)]
        static void Done(ShortcutArguments a)
        {
            var w = Window(a);
            if (w == null || Typing(w) || w.CurrentTab != KionjoWindow.Tab.Shoot) return;
            w.rootVisualElement.Q<ShootTab>()?.PressDone();
        }

        [Shortcut("Kionjo/Duplicate Shot", typeof(KionjoWindow), KeyCode.D, ShortcutModifiers.Action)]
        static void Duplicate(ShortcutArguments a)
        {
            var w = Window(a);
            if (w != null && !Typing(w)) w.DuplicateSelected();
        }

        [Shortcut("Kionjo/Remove Shot", typeof(KionjoWindow), KeyCode.Delete)]
        static void Remove(ShortcutArguments a)
        {
            var w = Window(a);
            if (w != null && !Typing(w) && w.CurrentTab == KionjoWindow.Tab.Storyboard) w.RemoveSelected();
        }

        [Shortcut("Kionjo/Move Shot Up", typeof(KionjoWindow), KeyCode.UpArrow, ShortcutModifiers.Action)]
        static void Up(ShortcutArguments a)
        {
            var w = Window(a);
            if (w != null && !Typing(w)) w.MoveSelected(-1);
        }

        [Shortcut("Kionjo/Move Shot Down", typeof(KionjoWindow), KeyCode.DownArrow, ShortcutModifiers.Action)]
        static void Down(ShortcutArguments a)
        {
            var w = Window(a);
            if (w != null && !Typing(w)) w.MoveSelected(1);
        }
    }
}
