using System.Collections.Generic;
using NUnit.Framework;
using SeedeXR.Kionjo.EditorTools;
using UnityEngine;

namespace SeedeXR.Kionjo.Tests
{
    public class CaptionWriterTests
    {
        KionjoBrandKit _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = ScriptableObject.CreateInstance<KionjoBrandKit>();
            _kit.identityLine = "Dome is a courtroom drama in VR, made by Studio.";
            _kit.callToAction = "Send this to a friend.";
            _kit.collaboratorHandle = "@studio";
            _kit.hashtags = new List<string> { "vr", "#gamedev", "indiedev" };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_kit);

        static JournalEntry Entry() => new() { title = "The ghost never turns", story = "Magic", achieved = "She never turns round. Ever." };

        static KionjoPost Post() => new() { hook = "Can a ghost refuse to turn?", overlays = { "Her head is locked." }, musicPath = "D:/m.wav" };

        [Test]
        public void Write_FullKit_ExactText()
        {
            Assert.AreEqual(
                "Can a ghost refuse to turn?\n\n" +
                "Dome is a courtroom drama in VR, made by Studio.\n\n" +
                "She never turns round. Ever.\n\n" +
                "Send this to a friend.\n\n" +
                "#vr #gamedev #indiedev\n\n" +
                "Alt text: The ghost never turns. Her head is locked.\n\n" +
                "Checklist:\n" +
                "- Invite @studio as collaborator\n" +
                "- Consider posting as a Trial Reel\n" +
                "- Add a trending sound in the app to the no-music version",
                CaptionWriter.Write(Entry(), Post(), _kit));
        }

        [Test]
        public void Write_SixHashtags_KeepsFive()
        {
            _kit.hashtags = new List<string> { "a", "b", "c", "d", "e", "f" };
            StringAssert.Contains("\n#a #b #c #d #e\n", CaptionWriter.Write(Entry(), Post(), _kit));
            StringAssert.DoesNotContain("#f", CaptionWriter.Write(Entry(), Post(), _kit));
        }

        [Test]
        public void Write_AchievedThreeSentences_KeepsTwo()
        {
            var e = Entry();
            e.achieved = "One. Two! Three.";
            var text = CaptionWriter.Write(e, Post(), _kit);
            StringAssert.Contains("\nOne. Two!\n", text);
            StringAssert.DoesNotContain("Three", text);
        }

        [Test]
        public void Write_NoCollaborator_NoInviteLine()
        {
            _kit.collaboratorHandle = "";
            StringAssert.DoesNotContain("Invite", CaptionWriter.Write(Entry(), Post(), _kit));
        }
    }
}
