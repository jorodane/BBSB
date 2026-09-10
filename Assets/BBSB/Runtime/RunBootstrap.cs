using BBSB.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BBSB.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RunBootstrap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingHealth = 100;
        [SerializeField, Min(0)] private int startingGold = 60;
        [SerializeField, Range(1, 100)] private int restPercent = 30;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int seed = 20260908;
        [Tooltip("Temporary battle result controls; disable after connecting the rhythm battle.")]
        [SerializeField] private bool showBattleTestControls = true;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            if (EventSystem.current == null)
            {
                var events = new GameObject("BBSB Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var font = Resources.Load<Font>("BBSB/Fonts/BBSBUI");
            if (font == null)
            {
                Debug.LogError("BBSB UI font is missing. Restore Assets/BBSB/Resources/BBSB/Fonts/BBSBUI.otf.");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            var presenter = gameObject.AddComponent<RunPresenter>();
            presenter.Initialize(new RunRules(startingHealth, startingGold, restPercent), font,
                useFixedSeed ? (int?)seed : null, showBattleTestControls);
        }
    }
}
