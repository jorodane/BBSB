using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    /// <summary>Owns lightweight sprite regions; all poses share eight imported atlas textures.</summary>
    public sealed class PlayerMotionSprites : IDisposable
    {
        public const string ResourcePath = "BBSB/BattleArt/PlayerMotion/";
        private readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
        private readonly Sprite fallbackPortrait;
        public bool UsesFallbackPortrait { get; }

        public PlayerMotionSprites()
        {
            try
            {
                Load("idle", 2, 2);
                Load("tap-left", 2, 2); Load("tap-right", 2, 2); Load("tap-upper", 2, 2);
                foreach (var id in new[] { "hold", "dive", "flick", "shake" }) Load(id, 3, 2);
                UsesFallbackPortrait = sheets.Count != 8;
                if (UsesFallbackPortrait)
                {
                    fallbackPortrait = Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");
                    if (fallbackPortrait == null)
                        throw new InvalidOperationException("Missing player motion atlases and fallback battle sprite: weapon-master");
                }
            }
            catch { Dispose(); throw; }
        }

        public Sprite Get(PlayerMotionFrame frame) => Get(frame.Sheet, frame.Index);
        public Sprite Get(string sheet, int index)
        {
            // Keep the existing portrait until the complete atlas set has been supplied.
            if (UsesFallbackPortrait) return fallbackPortrait;
            if (sheet == "tap")
            {
                string source = index / 4 == 0 ? "tap-left" : index / 4 == 1 ? "tap-right" : "tap-upper";
                return sheets[source][index % 4];
            }
            return sheets[sheet][index];
        }

        private void Load(string id, int columns, int rows)
        {
            var texture = Resources.Load<Texture2D>(ResourcePath + id);
            if (texture == null) return;
            var frames = new Sprite[columns * rows]; sheets.Add(id, frames);
            float width = (float)texture.width / columns, height = (float)texture.height / rows;
            for (int i = 0; i < frames.Length; i++)
            {
                int column = i % columns, row = i / columns;
                var rect = new Rect(column * width, texture.height - (row + 1) * height, width, height);
                var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .08f), 100, 0, SpriteMeshType.FullRect);
                sprite.name = "Player/" + id + "/" + i;
                frames[i] = sprite;
            }
        }

        public void Dispose()
        {
            foreach (var frames in sheets.Values)
            foreach (var sprite in frames)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            sheets.Clear();
        }
    }
}
