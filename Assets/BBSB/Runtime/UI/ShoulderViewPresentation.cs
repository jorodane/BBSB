using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    [CreateAssetMenu(menuName = "BBSB/Shoulder View Presentation")]
    public sealed class ShoulderViewPresentation : ScriptableObject
    {
        public const string ResourcePath = "BBSB/Presentation/ShoulderView";
        [HideInInspector] public int installedVersion;
        [HideInInspector] public string[] importedStageIds = Array.Empty<string>();
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
        public bool HasImportedStage(string id) => Array.IndexOf(importedStageIds ?? Array.Empty<string>(), id) >= 0;

        // The editor imports each available backdrop once. A later import must not
        // overwrite or recreate a map entry the artist has already edited/removed.
        public bool ImportStageOnce(string id, Texture2D backdrop, Color sky)
        {
            if (string.IsNullOrEmpty(id) || backdrop == null || HasImportedStage(id)) return false;
            var values = new List<Stage>(stages ?? Array.Empty<Stage>());
            if (!values.Exists(stage => stage != null && stage.id == id))
                values.Add(new Stage { id = id, backdrop = backdrop, sky = sky });
            stages = values.ToArray();
            var imported = new List<string>(importedStageIds ?? Array.Empty<string>()) { id };
            importedStageIds = imported.ToArray();
            return true;
        }
    }
}
