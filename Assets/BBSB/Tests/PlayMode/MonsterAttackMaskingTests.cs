using System.Collections;
using BBSB.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class MonsterAttackMaskingTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        { if (root != null) Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator AttackGraphicsHaveARendererBeforeClippingAndKeepItAcrossVisibilityChanges()
        {
            root = new GameObject("Attack masking test", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root.transform, false);
            ((RectTransform)viewport.transform).sizeDelta = new Vector2(400, 240);
            var mask = viewport.GetComponent<RectMask2D>();

            foreach (bool initiallyActive in new[] { true, false })
            {
                // Use bare RectTransforms, as the codex and battle slot factories do.
                var slot = new GameObject("Attack slot", typeof(RectTransform));
                slot.SetActive(initiallyActive); slot.transform.SetParent(viewport.transform, false);
                ((RectTransform)slot.transform).sizeDelta = new Vector2(100, 100);
                var graphic = slot.AddComponent<MonsterAttackGraphic>();
                // Check the actual component before any Graphic.canvasRenderer lazy lookup.
                var renderer = slot.GetComponent<CanvasRenderer>();
                Assert.IsTrue(renderer != null, "Adding the attack graphic must also add its CanvasRenderer.");
                Assert.AreEqual(1, slot.GetComponents<CanvasRenderer>().Length);
                graphic.raycastTarget = false;

                for (int cycle = 0; cycle < 3; cycle++)
                {
                    slot.SetActive(true);
                    Canvas.ForceUpdateCanvases(); mask.PerformClipping();
                    graphic.SetClipRect(new Rect(0, 0, 200, 120), true);
                    graphic.SetClipRect(Rect.zero, false);
                    yield return null;
                    Assert.AreSame(renderer, slot.GetComponent<CanvasRenderer>());
                    slot.SetActive(false);
                    Canvas.ForceUpdateCanvases(); mask.PerformClipping();
                    yield return null;
                }

                Object.Destroy(slot); yield return null;
                Canvas.ForceUpdateCanvases(); mask.PerformClipping();
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
