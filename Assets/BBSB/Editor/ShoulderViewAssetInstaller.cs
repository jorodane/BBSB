using System;
using System.Collections.Generic;
using System.IO;
using BBSB.Core;
using BBSB.Runtime;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BBSB.Editor
{
    // Paths are used only to import the delivered pack in the editor. The game uses
    // ordinary prefab, Animator, AnimationClip and Sprite object references.
    public static class ShoulderViewAssetInstaller
    {
        public const string ArtRoot = "Assets/BBSB/Art/ShoulderView";
        public const string NativeRoot = "Assets/BBSB/Presentation/ShoulderView";
        public const string PresentationPath = "Assets/BBSB/Resources/BBSB/Presentation/ShoulderView.asset";
        public const int Version = 1;
        public static readonly string[] MonsterIds = { "tap-slime", "march-slime", "tresillo-bat", "offbeat-goblin",
            "drowsy-slime", "clock-spirit", "seesaw-goblin", "spark-bat", "iron-turtle", "diving-ray", "bubble-spirit", "flick-goblin" };
        public static readonly string[] MapIds = { "POP", "RNB", "JAZZ", "RAP", "BARD", "CELT", "NEW", "METAL" };
        private static readonly string[] PlayerPoses = { "idle", "attack", "guard", "bow", "hurt" };
        private static readonly string[] MonsterPoses = { "idle", "windup", "attack" };
        private static readonly string[] Effects = { "parry", "guard", "slash", "arrow", "impact", "note-confirm", "note-shatter" };
        private static readonly Color[] SkyColors = {
            new Color(.57f, .78f, .92f), new Color(.12f, .09f, .22f), new Color(.29f, .35f, .44f), new Color(.40f, .54f, .63f),
            new Color(.55f, .74f, .87f), new Color(.61f, .74f, .74f), new Color(.77f, .87f, .87f), new Color(.25f, .16f, .24f) };
        private static bool queued, installing;

        [InitializeOnLoadMethod] private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Schedule();
        }
        private static void PlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.EnteredEditMode) Schedule(); }
        internal static void Schedule()
        {
            if (queued || installing) return;
            queued = true; EditorApplication.delayCall += AutoInstall;
        }
        private static void AutoInstall()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Schedule(); return; }
            var existing = AssetDatabase.LoadAssetAtPath<ShoulderViewPresentation>(PresentationPath);
            if (existing != null && existing.installedVersion >= Version)
            { if (!HasPendingStage(existing)) return; }
            else foreach (var path in RequiredActorImages()) if (!File.Exists(path)) return;
            if (!TryInstall(out var problem)) Debug.LogError("BBSB shoulder-view import: " + problem);
        }

        [MenuItem("BBSB/Presentation/Install shoulder-view art")]
        private static void InstallMenu()
        {
            if (!TryInstall(out var problem)) { Debug.LogError(problem); return; }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShoulderViewPresentation>(PresentationPath);
        }

        public static IEnumerable<string> RequiredImages()
        {
            foreach (var path in RequiredActorImages()) yield return path;
            foreach (string id in MapIds) yield return StagePath(id);
        }
        public static IEnumerable<string> RequiredActorImages()
        {
            foreach (string pose in PlayerPoses) yield return ArtRoot + "/Characters/Player/" + pose + ".png";
            foreach (string id in MonsterIds)
            {
                foreach (string pose in MonsterPoses) yield return ArtRoot + "/Characters/Monsters/" + id + "/" + pose + ".png";
                yield return ArtRoot + "/Projectiles/" + id + ".png";
            }
            foreach (string effect in Effects) yield return ArtRoot + "/Effects/" + effect + ".png";
        }

        public static bool TryInstall(out string problem)
        {
            problem = null;
            if (installing || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { problem = "Stop Play Mode and wait for script compilation before importing the art pack."; return false; }
            var art = AssetDatabase.LoadAssetAtPath<ShoulderViewPresentation>(PresentationPath);
            // Reimporting/replacing a PNG keeps its GUID and updates all existing references.
            // Never rebuild over someone's edited clips, prefabs or presentation settings.
            if (art != null && art.installedVersion >= Version) return TryInstallStages(art, out problem);
            var missing = new List<string>();
            foreach (var path in RequiredActorImages()) if (!File.Exists(path)) missing.Add(path);
            if (missing.Count > 0)
            { problem = "Copy the ZIP's Assets folder into the project. Missing " + missing.Count + " image(s):\n" + string.Join("\n", missing); return false; }
            var hero = AssetDatabase.LoadAssetAtPath<PlayerAuthoring>("Assets/BBSB/Resources/BBSB/Player.asset");
            var monsters = new List<MonsterAuthoring>();
            foreach (string id in MonsterIds)
            {
                var monster = AssetDatabase.LoadAssetAtPath<MonsterAuthoring>("Assets/BBSB/Resources/BBSB/Monsters/" + id + ".asset");
                if (monster == null) { problem = "Missing MonsterAuthoring: " + id; return false; }
                monsters.Add(monster);
            }
            if (hero == null) { problem = "Missing PlayerAuthoring. Use BBSB/Presentation/Create missing Canvas and actor prefabs first."; return false; }
            installing = true;
            try
            {
                foreach (var path in RequiredActorImages()) ImportTexture(path);
                EnsureFolder(NativeRoot);
                var playerSprites = Sprites("Characters/Player", PlayerPoses);
                var playerController = Controller("Player", new[] { "Idle", "TapImpact", "Guard", "Bow", "Hit" }, playerSprites);
                hero.visualPrefab = Prefab("Player", playerSprites[0], playerController);
                hero.controller = playerController; hero.portrait = playerSprites[0];
                hero.spriteReferenceHeight = playerSprites[0].bounds.size.y; hero.useLegacyFrames = false;
                foreach (var motion in hero.motions ?? Array.Empty<PlayerAuthoring.Motion>())
                {
                    if (motion == null) continue;
                    motion.state = "Base Layer." + (motion.phase == PlayerMotionPhase.Idle || motion.phase == PlayerMotionPhase.Recover ? "Idle" :
                        motion.gesture == GestureKind.Hold ? "Guard" : "TapImpact");
                }
                EditorUtility.SetDirty(hero);
                for (int i = 0; i < monsters.Count; i++)
                {
                    var monster = monsters[i]; var sprites = Sprites("Characters/Monsters/" + MonsterIds[i], MonsterPoses);
                    var controller = Controller(MonsterIds[i], new[] { "Idle", "Call", "Attack", "Recover", "Hit", "Perfect", "HalfMiss", "Miss", "Defeated" },
                        new[] { sprites[0], sprites[1], sprites[2], sprites[0], sprites[0], sprites[0], sprites[0], sprites[0], sprites[0] });
                    monster.actorPrefab = Prefab(MonsterIds[i], sprites[0], controller);
                    monster.visualPrefab = null; monster.controller = controller; monster.portrait = sprites[0];
                    monster.spriteReferenceHeight = sprites[0].bounds.size.y; monster.useLegacyBodyAnimation = false;
                    // Existing health, patterns, motion overrides and encounter weights are retained.
                    EditorUtility.SetDirty(monster);
                }
                if (art == null)
                {
                    art = ScriptableObject.CreateInstance<ShoulderViewPresentation>();
                    EnsureFolder(Path.GetDirectoryName(PresentationPath).Replace('\\', '/'));
                    AssetDatabase.CreateAsset(art, PresentationPath);
                }
                art.projectiles = new ShoulderViewPresentation.Projectile[monsters.Count];
                for (int i = 0; i < monsters.Count; i++) art.projectiles[i] = new ShoulderViewPresentation.Projectile {
                    monster = monsters[i], sprite = Sprite("Projectiles/" + MonsterIds[i]) };
                art.parry = Sprite("Effects/parry"); art.guard = Sprite("Effects/guard"); art.slash = Sprite("Effects/slash");
                art.arrow = Sprite("Effects/arrow"); art.impact = Sprite("Effects/impact");
                art.noteConfirm = Sprite("Effects/note-confirm"); art.noteShatter = Sprite("Effects/note-shatter");
                BindScreens(art);
                art.installedVersion = Version; EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
                // Commit actor bindings before touching optional backdrops. Missing or
                // invalid stage files cannot leave all character prefabs uninstalled.
                Debug.Log("BBSB shoulder-view art connected: 13 SpriteRenderer prefabs/Animator controllers, 12 projectiles and 7 effects. Native assets: " + NativeRoot);
                InstallStages(art);
                return true;
            }
            catch (Exception exception) { problem = exception.ToString(); return false; }
            finally { installing = false; }
        }

        private static string StagePath(string id) => ArtRoot + "/Stages/" + id + ".png";
        private static bool HasPendingStage(ShoulderViewPresentation art)
        {
            foreach (string id in MapIds) if (!art.HasImportedStage(id) && File.Exists(StagePath(id))) return true;
            return false;
        }
        private static bool TryInstallStages(ShoulderViewPresentation art, out string problem)
        {
            problem = null; installing = true;
            try { InstallStages(art); return true; }
            catch (Exception exception) { problem = exception.ToString(); return false; }
            finally { installing = false; }
        }
        private static void InstallStages(ShoulderViewPresentation art)
        {
            int added = 0;
            try
            {
                for (int i = 0; i < MapIds.Length; i++)
                {
                    string path = StagePath(MapIds[i]);
                    if (art.HasImportedStage(MapIds[i]) || !File.Exists(path)) continue;
                    ImportTexture(path);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null) throw new InvalidOperationException("Could not import stage texture: " + path);
                    if (art.ImportStageOnce(MapIds[i], texture, SkyColors[i])) added++;
                }
            }
            finally
            {
                if (added > 0) { EditorUtility.SetDirty(art); AssetDatabase.SaveAssets(); }
            }
            if (added > 0) Debug.Log("BBSB shoulder-view backdrops connected: " + added + " newly imported map(s).");
        }
        private static void ImportTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (importer == null) throw new InvalidOperationException("Could not import " + path);
            if (ConfigureTexture(importer, path)) importer.SaveAndReimport();
        }

        internal static bool ConfigureTexture(TextureImporter importer, string path)
        {
            bool stage = path.StartsWith(ArtRoot + "/Stages/", StringComparison.Ordinal);
            bool character = path.StartsWith(ArtRoot + "/Characters/", StringComparison.Ordinal);
            var type = stage ? TextureImporterType.Default : TextureImporterType.Sprite;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            int alignment = (int)(character ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            bool changed = importer.textureType != type || importer.mipmapEnabled || !importer.alphaIsTransparency ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear || importer.maxTextureSize != 2048 ||
                importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                (!stage && (importer.spriteImportMode != SpriteImportMode.Single || importer.spritePixelsPerUnit != 100 ||
                    settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteAlignment != alignment));
            if (!changed) return false;
            importer.textureType = type; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (!stage)
            {
                importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100;
                importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = alignment; settings.spritePivot = character ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
            }
            return true;
        }
        private static Sprite Sprite(string relative)
        {
            string path = ArtRoot + "/" + relative + ".png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Missing Sprite subasset: " + path);
        }
        private static Sprite[] Sprites(string relative, string[] poses)
        {
            var result = new Sprite[poses.Length];
            for (int i = 0; i < poses.Length; i++) result[i] = Sprite(relative + "/" + poses[i]);
            return result;
        }
        private static AnimatorController Controller(string actor, string[] states, Sprite[] sprites)
        {
            string folder = NativeRoot + "/" + actor; EnsureFolder(folder);
            string path = folder + "/" + actor + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            var clips = new AnimationClip[states.Length];
            for (int i = 0; i < states.Length; i++) clips[i] = Clip(folder, states[i], sprites[i]);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < states.Length; i++)
            {
                var state = machine.AddState(states[i], new Vector3((i % 3) * 230, (i / 3) * 90)); state.motion = clips[i];
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller); return controller;
        }
        private static AnimationClip Clip(string folder, string state, Sprite sprite)
        {
            string path = folder + "/" + state + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if (existing != null) return existing;
            var clip = new AnimationClip { name = state, frameRate = 30 };
            var binding = EditorCurveBinding.PPtrCurve("Body", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
                new ObjectReferenceKeyframe { time = 0, value = sprite }, new ObjectReferenceKeyframe { time = 1, value = sprite } });
            bool loop = state == "Idle" || state == "Guard" || state == "Bow";
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            float stretch = loop ? 1.018f : state == "Call" ? 1.04f : 1;
            Curve(clip, "m_LocalScale.y", new Keyframe(0, 1), new Keyframe(.5f, stretch), new Keyframe(1, 1));
            Curve(clip, "m_LocalScale.x", new Keyframe(0, 1), new Keyframe(.25f, state == "Attack" || state == "TapImpact" ? 1.045f : 1), new Keyframe(1, 1));
            Curve(clip, "m_LocalPosition.x", new Keyframe(0, 0), new Keyframe(.2f, state == "Hit" ? -.15f : 0), new Keyframe(1, 0));
            AssetDatabase.CreateAsset(clip, path); return clip;
        }
        private static void Curve(AnimationClip clip, string property, params Keyframe[] keys) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Body", typeof(Transform), property), new AnimationCurve(keys));
        private static GameObject Prefab(string actor, Sprite idle, RuntimeAnimatorController controller)
        {
            string path = NativeRoot + "/" + actor + "/" + actor + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject(actor, typeof(Animator));
            try
            {
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                var body = new GameObject("Body", typeof(SpriteRenderer)); body.transform.SetParent(root.transform, false);
                var renderer = body.GetComponent<SpriteRenderer>(); renderer.sprite = idle; renderer.sortingOrder = 1;
                return PrefabUtility.SaveAsPrefabAsset(root, path) ?? throw new InvalidOperationException("Could not save " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void BindScreens(ShoulderViewPresentation art)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BBSB/Resources/BBSB/Presentation" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponentInChildren<FiveLaneHudBindings>(true) == null) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var hud = contents.GetComponentInChildren<FiveLaneHudBindings>(true);
                    hud.presentation = art; hud.EnsureStageLayout(); PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    public sealed class ShoulderViewArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ShoulderViewAssetInstaller.ArtRoot + "/", StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
            ShoulderViewAssetInstaller.ConfigureTexture((TextureImporter)assetImporter, assetPath);
        }
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
                if (path.StartsWith(ShoulderViewAssetInstaller.ArtRoot + "/", StringComparison.Ordinal))
                { ShoulderViewAssetInstaller.Schedule(); return; }
            foreach (string path in moved)
                if (path.StartsWith(ShoulderViewAssetInstaller.ArtRoot + "/", StringComparison.Ordinal))
                { ShoulderViewAssetInstaller.Schedule(); return; }
        }
    }
}
