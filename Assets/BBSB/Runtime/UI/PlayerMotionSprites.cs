using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    /// <summary>Uses the artist's named Sprite Editor slices, including their rectangles and pivots.</summary>
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

        public Sprite Get(PlayerMotionFrame frame) => Get(frame.SourceSheet, frame.SourceIndex);
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
            var imported = Resources.LoadAll<Sprite>(ResourcePath + id);
            var frames = new Sprite[columns * rows];
            for (int i = 0; i < frames.Length; i++)
            {
                string name = id + "_" + i;
                foreach (var sprite in imported)
                    if (sprite.name == name) { frames[i] = sprite; break; }
                // A Single-mode image or incomplete slicing is not a complete animation sheet.
                if (frames[i] == null) return;
            }
            sheets.Add(id, frames);
        }

        public void Dispose()
        {
            // Resources owns imported sprites; destroying one would break the next arena/preview.
            sheets.Clear();
        }
    }
}
