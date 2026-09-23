using UnityEngine;

namespace BBSB.Runtime.UI
{
    [CreateAssetMenu(menuName = "BBSB/Battle Visual Theme")]
    public sealed class BattleVisualTheme : ScriptableObject
    {
        public const string ResourcePath = "BBSB/Presentation/CathedralBattle";
        [Tooltip("Use the new arena in five-lane combat. Disable to keep each map's existing backdrop.")]
        public bool useArena = true;
        public Texture2D arena, skyClouds;
        public Sprite lightNote, darkNote, healthFrame, comboCrest, beatRing, weaponHalo, judgmentFlash;
        public Color sky = new Color(.22f, .27f, .36f);
        public static readonly Color Light = new Color(1, .76f, .43f);
        public static readonly Color Dark = new Color(.65f, .31f, 1);
        public static Color NoteColor(double beat) => BattleBoardLayout.IsOffbeat(beat) ? Dark : Light;
        public Sprite NoteSprite(double beat) => BattleBoardLayout.IsOffbeat(beat) ? darkNote : lightNote;
    }
}
