using System.Collections;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BBSB.Tests
{
    public sealed class FiveLanePlaybackTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (root != null) Object.Destroy(root); yield return null; }

        [UnityTest] public IEnumerator DefaultBootstrapOpensFiveLanesAndWaitsForHeldContactOnResume()
        {
            root = new GameObject("Five lane bootstrap"); root.AddComponent<RunBootstrap>();
            yield return null;
            var start = root.GetComponentsInChildren<Button>().First(b =>
                b.GetComponentInChildren<TMP_Text>() != null && b.GetComponentInChildren<TMP_Text>().text == "탐험 시작");
            start.onClick.Invoke();
            var presenter = root.GetComponent<RunPresenter>();
            Assert.IsTrue(presenter.Session.UsesFiveLaneCombat);
            Assert.IsTrue(presenter.Session.Enter(presenter.Session.Map.Nodes.First(n => presenter.Session.CanEnter(n.Id)).Id));
            Assert.IsTrue(presenter.StartFiveLaneBattle());
            var playback = root.GetComponentInChildren<FiveLanePlayback>();
            Assert.IsNotNull(playback);
            Assert.AreEqual(5, playback.GetComponentsInChildren<FiveLaneInputSurface>().Length);
            Assert.IsNotNull(playback.GetComponentInChildren<FiveLaneTrackGraphic>());
            var battle = playback.Battle;
            battle.Press(3, 0); battle.Advance(.5);
            playback.Pause(); Assert.IsTrue(battle.IsPaused);
            playback.Continue(); Assert.IsTrue(playback.WaitingForHold); Assert.IsTrue(battle.IsPaused);
            playback.SetPointer(3, true);
            Assert.IsFalse(playback.WaitingForHold); Assert.IsFalse(battle.IsPaused);
            Assert.IsTrue(battle.Lanes[3].Holding); Assert.AreEqual(.5, battle.Beat);
            Assert.AreSame(battle, presenter.Session.PhraseBattle);
        }
    }
}
