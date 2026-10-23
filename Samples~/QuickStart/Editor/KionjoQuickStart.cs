using System.IO;
using SeedeXR.Kionjo;
using SeedeXR.Kionjo.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Tools > Kionjo > Build Quick Start: a small scene with a stand in rig and an example storyboard.</summary>
public static class KionjoQuickStart
{
    [MenuItem("Tools/Kionjo/Build Quick Start Scene")]
    public static void Build()
    {
        var folder = "Assets/Kionjo Quick Start";
        Directory.CreateDirectory(folder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);

        var rig = new GameObject("Rig");
        var head = new GameObject("Head").AddComponent<Camera>();
        head.tag = "MainCamera";
        head.transform.SetParent(rig.transform);
        head.transform.localPosition = new Vector3(0, 1.6f, -4f);
        head.gameObject.AddComponent<AudioListener>();
        Transform Hand(string name, float x)
        {
            var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
            h.name = name;
            h.transform.SetParent(rig.transform);
            h.transform.localScale = Vector3.one * 0.08f;
            h.transform.localPosition = new Vector3(x, 1.2f, -3.6f);
            return h.transform;
        }
        var left = Hand("LeftHand", -0.25f);
        var right = Hand("RightHand", 0.25f);
        var rec = rig.AddComponent<KionjoRecorder>();
        rec.tracks.Add(new KionjoTrack { name = "Head", target = head.transform });
        rec.tracks.Add(new KionjoTrack { name = "LeftHand", target = left });
        rec.tracks.Add(new KionjoTrack { name = "RightHand", target = right });
        var mover = rig.AddComponent<KionjoSampleRigMover>();
        mover.head = head.transform;
        mover.leftHand = left;
        mover.rightHand = right;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = Vector3.one * 4f;
        var hero = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hero.name = "Hero Object";
        hero.transform.position = new Vector3(0, 0, 6);
        hero.transform.localScale = Vector3.one * 6f;
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Debug Panel";
        panel.transform.position = new Vector3(2, 1.5f, 1);
        panel.transform.localScale = new Vector3(1.2f, 0.7f, 0.05f);
        EditorSceneManager.SaveScene(scene, $"{folder}/Kionjo Quick Start.unity");

        var sb = ScriptableObject.CreateInstance<KionjoStoryboard>();
        sb.title = "My Trailer";
        sb.targetSeconds = 30f;
        var open = sb.AddShot(KionjoShotType.Card);
        open.name = "Opening line";
        open.cardKind = KionjoCardKind.Text;
        open.cardLines.Add("One line that sets up the world.");
        open.targetSeconds = 4f;
        var wide = sb.AddShot();
        wide.name = "Wide shot of the place";
        wide.location = "outside";
        wide.beat = "AND";
        wide.targetSeconds = 6f;
        wide.hidePaths.Add("/Debug Panel");
        var close = sb.AddShot();
        close.name = "Look at the hero object";
        close.location = "outside";
        close.beat = "BUT";
        close.targetSeconds = 5f;
        var end = sb.AddShot(KionjoShotType.Card);
        end.name = "Title";
        end.cardKind = KionjoCardKind.Text;
        end.cardLines.Add("My Trailer");
        end.cardLines.Add("Coming soon");
        end.targetSeconds = 4f;
        AssetDatabase.CreateAsset(sb, $"{folder}/My Trailer.asset");
        AssetDatabase.SaveAssets();
        KionjoWindow.Open().Storyboard = sb;
        Debug.Log("Kionjo: Quick Start built. Enter Play mode, open the Shoot tab, press Space twice to record a take, then Render and Cut.");
    }
}
