using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>Marks a string field as a file path: shown with a Browse button and accepts dropped files or assets.</summary>
    public class KionjoPathAttribute : PropertyAttribute
    {
        public readonly string Extensions;
        public KionjoPathAttribute(string extensions = "") => Extensions = extensions;
    }

    [CustomPropertyDrawer(typeof(KionjoPathAttribute))]
    class KionjoPathDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var ext = ((KionjoPathAttribute)attribute).Extensions;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var field = new TextField(property.displayName) { tooltip = property.tooltip, bindingPath = property.propertyPath };
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.BindProperty(property);
            var browse = new Button(() =>
            {
                var current = field.value;
                var dir = string.IsNullOrEmpty(current) ? "" : Path.GetDirectoryName(current);
                var picked = EditorUtility.OpenFilePanel(property.displayName, dir, ext.Split(',')[0]);
                if (!string.IsNullOrEmpty(picked)) field.value = picked;
            }) { text = "Browse", tooltip = "Pick a file. You can also drop a file or an asset on the field." };
            row.Add(field);
            row.Add(browse);

            field.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DropPath() != null) DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });
            field.RegisterCallback<DragPerformEvent>(_ =>
            {
                var path = DropPath();
                if (path == null) return;
                DragAndDrop.AcceptDrag();
                field.value = path;
            });
            return row;
        }

        /// <summary>An absolute path for the first dropped file or asset, null when nothing usable is dragged.</summary>
        static string DropPath()
        {
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                var p = DragAndDrop.paths[0];
                return Path.IsPathRooted(p) ? p.Replace('\\', '/') : Path.GetFullPath(p).Replace('\\', '/');
            }
            return null;
        }
    }
}
