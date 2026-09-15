using BBSB.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class TextMeshProPresentationTests
    {
        [Test]
        public void AuthoredScreenBindingsAndButtonLabelsSurviveTmpMigration()
        {
            var library = Resources.Load<PresentationPrefabs>(PresentationPrefabs.ResourcePath);
            Assert.IsNotNull(library);
            var host = new GameObject("TMP test canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                foreach (var entry in library.screens)
                {
                    var screen = library.Create(entry.kind, host.transform);
                    Assert.IsNotNull(screen.content, entry.kind.ToString());
                    Assert.IsEmpty(screen.GetComponentsInChildren<Text>(true), entry.kind.ToString());
                    if (entry.kind == RunScreenKind.Title) Assert.IsTrue(screen.title.IsValid);
                    if (entry.kind == RunScreenKind.Preparation) Assert.IsTrue(screen.preparation.IsValid);
                    if (entry.kind == RunScreenKind.Battle) Assert.IsTrue(screen.battle.IsValid);
                    foreach (var button in screen.GetComponentsInChildren<Button>(true))
                        Assert.IsNotNull(button.GetComponentInChildren<TextMeshProUGUI>(true), button.name);
                    foreach (var text in screen.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        Assert.IsNotNull(text.font, text.name);
                        Assert.IsFalse(text.raycastTarget, text.name);
                    }
                    Object.DestroyImmediate(screen.gameObject);
                }
                Assert.IsNotNull(library.titleText); Assert.IsNotNull(library.headingText);
                Assert.IsNotNull(library.bodyText); Assert.IsNotNull(library.captionText);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ProjectTmpFontRendersKoreanAndDigits()
        {
            var font = PresentationFonts.Load();
            Assert.IsNotNull(font);
            Assert.IsNotNull(font.sourceFontFile);
            const string sample = "무기 몬스터 도감 준비 연주 시작 퍼펙트 반미스 COMBO 0123456789";
            Assert.IsTrue(font.HasCharacters(sample, out uint[] missing, false, true),
                "BBSBUI 폰트에 필요한 글자가 없어: " + string.Join(",", missing));
        }

        [Test]
        public void CompletedFontSetupKeepsAuthoredTmpStyle()
        {
            var library = ScriptableObject.CreateInstance<PresentationPrefabs>();
            var root = new GameObject("Styled text", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                library.textMeshProVersion = 1;
                library.defaultFont = PresentationFonts.Load();
                var label = root.GetComponent<TextMeshProUGUI>();
                var font = TMP_Settings.defaultFontAsset;
                label.font = font; label.fontSize = 31; label.characterSpacing = 4;
                label.color = Color.magenta; label.alignment = TextAlignmentOptions.TopRight;
                var material = label.fontSharedMaterial;
                library.PrepareText(root);
                Assert.AreSame(font, label.font); Assert.AreSame(material, label.fontSharedMaterial);
                Assert.AreEqual(31, label.fontSize); Assert.AreEqual(4, label.characterSpacing);
                Assert.AreEqual(Color.magenta, label.color); Assert.AreEqual(TextAlignmentOptions.TopRight, label.alignment);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(library); }
        }
    }
}
