using System;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    [CreateAssetMenu(menuName = "BBSB/Shoulder View Presentation")]
    public sealed class ShoulderViewPresentation : ScriptableObject
    {
        public const string ResourcePath = "BBSB/Presentation/ShoulderView";
        [HideInInspector] public int installedVersion;
        public Stage[] stages = Array.Empty<Stage>();
        public Projectile[] projectiles = Array.Empty<Projectile>();
        public Sprite parry, guard, slash, arrow, impact, noteConfirm, noteShatter;

        [Serializable] public sealed class Stage
        {
            [Tooltip("A song ID such as BARD-06, or a whole map such as BARD.")]
            public string id;
            public Texture2D backdrop;
            public Color sky = new Color(.09f, .13f, .22f);
        }
        [Serializable] public sealed class Projectile
        {
            public MonsterAuthoring monster;
            public Sprite sprite;
        }
        public Stage FindStage(string stageId, string mapId)
        {
            foreach (var stage in stages ?? Array.Empty<Stage>()) if (stage != null && stage.id == stageId && stage.backdrop != null) return stage;
            foreach (var stage in stages ?? Array.Empty<Stage>()) if (stage != null && stage.id == mapId && stage.backdrop != null) return stage;
            return null;
        }
        public Sprite FindProjectile(string monsterId)
        {
            foreach (var projectile in projectiles ?? Array.Empty<Projectile>())
                if (projectile != null && projectile.monster != null && projectile.monster.monsterId == monsterId) return projectile.sprite;
            return null;
        }
    }
}
