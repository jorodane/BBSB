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
        public void ScreenPrefabsFillTheSafeAreaAtBothLandscapeRatios()
        {
            var library = Resources.Load<PresentationPrefabs>(PresentationPrefabs.ResourcePath);
            Assert.IsNotNull(library);
            var host = new GameObject("Screen geometry canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var hostRect = (RectTransform)host.transform; hostRect.localScale = Vector3.one;
            var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(hostRect, false); safe.anchorMin = new Vector2(.03f, .05f); safe.anchorMax = new Vector2(.97f, .95f);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            try
            {
                foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800) })
                {
                    hostRect.sizeDelta = size;
                    foreach (var entry in library.screens)
                    {
                        var screen = library.Create(entry.kind, safe);
                        var rect = (RectTransform)screen.transform;
                        Assert.AreEqual(Vector3.one, rect.localScale, entry.kind.ToString());
                        AssertWorldSize(rect, safe.rect.size);
                        Assert.AreSame(canvas, screen.GetComponent<Canvas>().rootCanvas);
                        if (entry.kind == RunScreenKind.Title)
                            foreach (var label in screen.GetComponentsInChildren<TextMeshProUGUI>())
                            {
                                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                                Assert.Greater(Vector3.Distance(corners[0], corners[3]), 1, label.name + " collapsed horizontally");
                                Assert.Greater(Vector3.Distance(corners[0], corners[1]), 1, label.name + " collapsed vertically");
                            }
                        Object.DestroyImmediate(screen.gameObject);
                    }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void LegacyZeroScaleScreenIsRecoveredWithoutOverwritingItsAuthoredChildren()
        {
            var library = ScriptableObject.CreateInstance<PresentationPrefabs>();
            var host = new GameObject("Screen parent", typeof(RectTransform));
            var source = new GameObject("Legacy screen", typeof(RectTransform), typeof(CanvasScreen));
            try
            {
                var hostRect = (RectTransform)host.transform; hostRect.sizeDelta = new Vector2(1280, 720);
                var sourceRect = (RectTransform)source.transform; sourceRect.localScale = Vector3.zero;
                var template = source.GetComponent<CanvasScreen>();
                template.content = new GameObject("Authored content", typeof(RectTransform)).GetComponent<RectTransform>();
                template.content.SetParent(sourceRect, false);
                template.content.anchorMin = new Vector2(.12f, .15f); template.content.anchorMax = new Vector2(.82f, .9f);
                template.content.offsetMin = new Vector2(13, 17); template.content.offsetMax = new Vector2(-19, -23);
                template.content.localScale = new Vector3(.8f, .9f, 1);
                library.screens = new[] { new PresentationPrefabs.Screen { kind = RunScreenKind.Title, prefab = template } };
                var screen = library.Create(RunScreenKind.Title, hostRect);
                AssertWorldSize((RectTransform)screen.transform, hostRect.rect.size);
                Assert.AreEqual(Vector3.zero, sourceRect.localScale, "The source prefab must stay untouched during instantiation.");
                Assert.AreEqual(template.content.anchorMin, screen.content.anchorMin);
                Assert.AreEqual(template.content.anchorMax, screen.content.anchorMax);
                Assert.AreEqual(template.content.offsetMin, screen.content.offsetMin);
                Assert.AreEqual(template.content.offsetMax, screen.content.offsetMax);
                Assert.AreEqual(template.content.localScale, screen.content.localScale);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(source); Object.DestroyImmediate(library); }
        }

        private static void AssertWorldSize(RectTransform rect, Vector2 expected)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Assert.AreEqual(expected.x, Vector3.Distance(corners[0], corners[3]), .1f, rect.name + " visible width");
            Assert.AreEqual(expected.y, Vector3.Distance(corners[0], corners[1]), .1f, rect.name + " visible height");
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
