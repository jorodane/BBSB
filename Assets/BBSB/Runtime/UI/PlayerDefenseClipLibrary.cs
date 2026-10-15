using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    // The JSON manifest is tracked independently of the optional art ZIP. Each
    // action owns six parts; any part may be replaced with several drawn frames.
    internal sealed class PlayerDefenseClipLibrary : IDisposable
    {
        public const string ResourcePath = "BBSB/DefenseMotionClips";
        [Serializable] private sealed class Definition { public Atlas[] atlases = Array.Empty<Atlas>(); public Clip[] clips = Array.Empty<Clip>(); }
        [Serializable] private sealed class Atlas
        {
            public string id = "";
            public float width, height, bodyHeight;
            public Frame[] frames = Array.Empty<Frame>();
        }
        [Serializable] private sealed class Frame { public Vector4 bounds; public Vector2 feet; public float bodyHeight; }
        [Serializable] private sealed class Clip
        {
            public int directions;
            public string atlas = "";
            public Part[] parts = Array.Empty<Part>();
        }
        [Serializable] private sealed class Part { public int[] frames = Array.Empty<int>(); }
        private readonly Dictionary<int, Sprite[][]> clips = new Dictionary<int, Sprite[][]>();
        private readonly List<Sprite> owned = new List<Sprite>();
        public int Count => clips.Count;

        public void Load()
        {
            var manifest = Resources.Load<TextAsset>(ResourcePath);
            if (manifest == null) return;
            Definition data;
            try { data = JsonUtility.FromJson<Definition>(manifest.text); }
            catch (ArgumentException error) { Debug.LogError("Invalid defense clip manifest: " + error.Message); return; }
            var sheets = new Dictionary<string, Sprite[]>();
            foreach (var sheet in data.atlases)
            {
                var texture = Resources.Load<Texture2D>("BBSB/DefenseMotion/" + sheet.id);
                if (texture == null) continue;
                if (sheet.width <= 0 || sheet.height <= 0 || sheet.bodyHeight <= 0 ||
                    Mathf.Abs(texture.width / sheet.width - texture.height / sheet.height) > .01f)
                { Debug.LogError("Defense atlas size differs from its manifest: " + sheet.id); continue; }
                float scale = texture.width / sheet.width;
                var sprites = new Sprite[sheet.frames.Length];
                for (int i = 0; i < sprites.Length; i++)
                {
                    var f = sheet.frames[i]; var b = f.bounds;
                    if (b.x < 0 || b.y < 0 || b.z > sheet.width || b.w > sheet.height || b.z <= b.x || b.w <= b.y)
                    { Debug.LogError("Invalid defense frame bounds: " + sheet.id + ":" + i); continue; }
                    var rect = new Rect(b.x * scale, texture.height - b.w * scale, (b.z - b.x) * scale, (b.w - b.y) * scale);
                    var pivot = new Vector2((f.feet.x - b.x) / (b.z - b.x), (b.w - f.feet.y) / (b.w - b.y));
                    var sprite = Sprite.Create(texture, rect, pivot, (f.bodyHeight > 0 ? f.bodyHeight : sheet.bodyHeight) * scale, 0, SpriteMeshType.FullRect);
                    sprite.name = "Defense " + sheet.id + ":" + i;
                    sprites[i] = sprite; owned.Add(sprite);
                }
                sheets[sheet.id] = sprites;
            }
            foreach (var clip in data.clips)
            {
                if (clip.directions < 1 || clip.directions > 15 || clips.ContainsKey(clip.directions) || clip.parts.Length != 6)
                { Debug.LogError("Defense clips require unique directions and six separate parts."); continue; }
                if (!sheets.TryGetValue(clip.atlas, out var sprites)) continue;
                var parts = new Sprite[6][]; bool valid = true;
                for (int p = 0; p < parts.Length; p++)
                {
                    var indices = clip.parts[p].frames; parts[p] = new Sprite[indices.Length];
                    if (indices.Length == 0) valid = false;
                    for (int i = 0; i < indices.Length; i++)
                    {
                        int index = indices[i];
                        if (index < 0 || index >= sprites.Length || sprites[index] == null) valid = false;
                        else parts[p][i] = sprites[index];
                    }
                }
                if (valid) clips.Add(clip.directions, parts);
                else Debug.LogError("Missing part of defense clip: " + clip.directions);
            }
        }

        public Sprite Sample(PlayerDefenseFrame frame)
        {
            int part = DefenseMotionCatalog.Part(frame);
            if (part < 0 || !clips.TryGetValue(frame.Directions, out var parts)) return null;
            double start = part == 0 ? 0 : part == 1 ? PlayerDefenseMotion.WindupEnd : part == 2 ? PlayerDefenseMotion.SmearEnd :
                part == 3 ? .205 : part == 4 ? PlayerDefenseMotion.ContactEnd : .38;
            double end = part == 0 ? PlayerDefenseMotion.WindupEnd : part == 1 ? PlayerDefenseMotion.SmearEnd :
                part == 2 ? .205 : part == 3 ? PlayerDefenseMotion.ContactEnd : part == 4 ? .38 : PlayerDefenseMotion.RecoverEnd;
            if (frame.Releasing) { start = 0; end = .22; }
            var frames = parts[part];
            int index = frame.Phase == DefenseMotionPhase.Hold ? 0 : Mathf.Min(frames.Length - 1,
                Mathf.FloorToInt(Mathf.Clamp01((float)((frame.Age - start) / (end - start))) * frames.Length));
            return frames[index];
        }
        public void Dispose()
        {
            foreach (var sprite in owned) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            owned.Clear(); clips.Clear();
        }
    }
}
