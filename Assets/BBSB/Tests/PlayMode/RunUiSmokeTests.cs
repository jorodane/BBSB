using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class RunUiSmokeTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesKoreanUIAndStartButtonOpensMap()
        {
            root = new GameObject("UI smoke test");
            root.AddComponent<RunBootstrap>();
            yield return null;
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsNotNull(presenter);
            var font = Resources.Load<Font>("BBSB/Fonts/BBSBUI");
            Assert.IsNotNull(font);
            Assert.IsTrue(font.HasCharacter('탐'));
            var start = root.GetComponentsInChildren<Button>().Single(x => x.GetComponentInChildren<Text>().text == "탐험 시작");
            start.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(RunPhase.Map, presenter.Session.Phase);
            Assert.AreEqual(5, presenter.Session.Weapons.Count);
            var nodes = root.GetComponentsInChildren<Button>().Where(x => x.GetComponentInChildren<Text>().text.Contains("진입")).ToArray();
            Assert.AreEqual(3, nodes.Length);
            foreach (var node in nodes)
            {
                Assert.IsTrue(node.interactable);
                Assert.Greater(((RectTransform)node.transform).rect.height, 40);
                Assert.Greater(((RectTransform)node.transform).rect.width, 40);
            }
            nodes[0].onClick.Invoke();
            yield return null;
            Assert.AreEqual(RunPhase.Stage, presenter.Session.Phase);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
