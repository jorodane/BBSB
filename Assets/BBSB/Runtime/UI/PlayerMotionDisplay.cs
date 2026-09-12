using System;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    /// <summary>Artist-owned ground position and uniform size calibration for the existing poses.</summary>
    [CreateAssetMenu(menuName = "BBSB/Player Motion Display")]
    public sealed class PlayerMotionDisplay : ScriptableObject
    {
        public const string ResourcePath = "BBSB/BattleArt/PlayerMotionDisplay";
        public static readonly Vector2 DefaultGround = new Vector2(.14f, .15f);
        public const float DefaultCharacterScale = .5f;

        [Tooltip("Player's ground point in arena coordinates, from bottom-left (0,0) to top-right (1,1).")]
        public Vector2 groundPosition = DefaultGround;
        [Min(.01f), Tooltip("Uniform size of the whole character, including every pose.")]
        public float characterScale = DefaultCharacterScale;
        [Range(0, 2), Tooltip("Brief squash and stretch during battle. 0 disables deformation; 1 is the default intensity.")]
        public float squashStretchStrength = PlayerSquashStretch.DefaultStrength;
        [Tooltip("Standing pose used as the common size reference. Empty uses idle_0.")]
        public Sprite referencePose;
        public Sheet[] sheets = Array.Empty<Sheet>();

        [Serializable]
        public sealed class Sheet
        {
            public string name;
            [Min(.01f)] public float scale = 1;
            public Pose[] poses = Array.Empty<Pose>();
        }

        [Serializable]
        public sealed class Pose
        {
            public string name;
            [Min(.01f)] public float scale = 1;
            [Tooltip("In reference-character heights. X adjusts stance; Y is deliberate elevation (0 stays grounded).")]
            public Vector2 offset;
        }

        public Sheet FindSheet(string name)
        {
            if (sheets != null) foreach (var sheet in sheets)
                if (sheet != null && sheet.name == name) return sheet;
            return null;
        }

        public void GetCalibration(string sheetName, int index, out float scale, out Vector2 offset)
        {
            scale = 1; offset = Vector2.zero;
            var sheet = FindSheet(sheetName);
            if (sheet == null) return;
            scale = ValidScale(sheet.scale);
            if (sheet.poses == null || index < 0 || index >= sheet.poses.Length || sheet.poses[index] == null) return;
            scale *= ValidScale(sheet.poses[index].scale);
            offset = sheet.poses[index].offset;
        }

        public static Vector2 Measure(Sprite sprite, Sprite reference, float referenceDisplayHeight, float scale)
        {
            var geometry = PlayerPoseGeometry.Calculate(sprite.rect.width / sprite.pixelsPerUnit,
                sprite.rect.height / sprite.pixelsPerUnit, reference.rect.height / reference.pixelsPerUnit,
                referenceDisplayHeight, ValidScale(scale));
            return new Vector2((float)geometry.Width, (float)geometry.Height);
        }

        public static float ReferenceDisplayHeight(Vector2 arenaSize, float scale) =>
            Mathf.Min(arenaSize.x * .4f, arenaSize.y * .7f) * ValidScale(scale);

        private static float ValidScale(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Max(.01f, value);

        private void OnValidate()
        {
            groundPosition = new Vector2(Mathf.Clamp01(groundPosition.x), Mathf.Clamp01(groundPosition.y));
            characterScale = ValidScale(characterScale);
            squashStretchStrength = float.IsNaN(squashStretchStrength) || float.IsInfinity(squashStretchStrength) ?
                PlayerSquashStretch.DefaultStrength : Mathf.Clamp(squashStretchStrength, 0, 2);
        }
    }
}
