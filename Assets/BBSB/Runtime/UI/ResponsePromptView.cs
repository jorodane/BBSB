using TMPro;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsePromptView : MonoBehaviour
    {
        private readonly GestureKind[] kinds = new GestureKind[5];
        private readonly GestureIconGraphic[] icons = new GestureIconGraphic[5];
        private TMP_FontAsset font;
        public int VisibleCount { get; private set; }
        internal void Initialize(TMP_FontAsset value)
        {
            font = value;
            for (int i = 0; i < icons.Length; i++)
            { icons[i] = GestureIconGraphic.Create(transform, GestureKind.Tap, true, font); icons[i].gameObject.SetActive(false); }
        }
        public void Refresh(RhythmRound round, string monsterId, double seconds)
        {
            VisibleCount = ResponsePromptTimeline.Fill(round, monsterId, seconds, kinds);
            Layout();
        }
        internal void Layout()
        {
            float side = Mathf.Min(64, ((RectTransform)transform).rect.width / Mathf.Max(1, VisibleCount));
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i]; bool visible = i < VisibleCount;
                if (icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
                if (!visible) continue;
                if (icon.Kind != kinds[i]) icon.Bind(kinds[i], true, font);
                RunUI.Pin(icon.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f),
                    new Vector2((i-(VisibleCount-1)*.5f)*side,0),new Vector2(side,side));
            }
        }
    }
}
