using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Idle portrait or the real battle presentation, driven by the codex's isolated input round.</summary>
    public sealed class MonsterCodexStage : MonoBehaviour
    {
        private RunUI ui;
        private MonsterDefinition monster;
        private MonsterPreview preview;
        private MonsterAttackSprites sprites;
        private Image body;
        private Text missing;
        private Sprite fallback;
        private BattleArenaView arena;
        private RhythmRound boundRound;
        public BattleArenaView Arena => arena;

        internal void Bind(RunUI ui, MonsterDefinition monster, MonsterAttackSprites sprites)
        {
            this.ui = ui; this.monster = monster; this.sprites = sprites;
            fallback = Resources.Load<Sprite>("BBSB/BattleArt/" + monster.ArtId);
            ui.Background((RectTransform)transform, RunUI.Panel, true);
            var portrait = ui.Rect("Monster body", transform);
            body = ui.Background(portrait, Color.white); body.preserveAspect = false;
            missing = ui.Label(transform, "모습 준비 중", 24, RunUI.Muted, 60, TextAnchor.MiddleCenter);
            RunUI.Stretch(missing.rectTransform);
        }

        internal void Select(MonsterPreview value)
        {
            preview = value; boundRound = null;
            if (arena != null) { arena.gameObject.SetActive(false); Destroy(arena.gameObject); arena = null; }
            body.gameObject.SetActive(value == null); missing.gameObject.SetActive(value == null);
            if (value == null) return;
            var root = ui.Rect("Interactive monster rehearsal", transform); RunUI.Stretch(root);
            arena = root.gameObject.AddComponent<BattleArenaView>();
            arena.Initialize(value.Round, ui.Font); arena.HideLabels(); boundRound = value.Round;
        }

        internal void Refresh(double seconds, bool response)
        {
            if (preview != null)
            {
                if (!ReferenceEquals(boundRound, preview.Round)) { arena.Repeat(preview.Round); boundRound = preview.Round; }
                arena.Refresh(); return;
            }
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            var portrait = sprites.Get(MonsterAttackDefinition.ResourceRoot + monster.Id, "idle", seconds * 4) ?? fallback;
            body.sprite = portrait; body.enabled = portrait != null; missing.enabled = portrait == null;
            if (portrait == null) return;
            float aspect = portrait.rect.width / portrait.rect.height;
            var pivot = new Vector2(portrait.pivot.x / portrait.rect.width, portrait.pivot.y / portrait.rect.height);
            float height = Mathf.Min(size.y * .92f, size.x * .88f / aspect);
            RunUI.Pin(body.rectTransform, Vector2.zero, pivot, new Vector2(size.x * .5f, size.y * .05f), new Vector2(height * aspect, height));
        }
    }
}
