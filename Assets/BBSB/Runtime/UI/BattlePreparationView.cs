using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>A still versus composition. Opening it never advances the battle clock or changes the loadout.</summary>
    public sealed class BattlePreparationView : MonoBehaviour
    {
        public const string ArtRoot = "BBSB/PreparationArt/";
        private RunUI ui;
        private RectTransform root;
        private readonly List<Image> portraits = new List<Image>();
        public Image HeroPortrait { get; private set; }
        public IReadOnlyList<Image> MonsterPortraits => portraits;
        public Button StartButton { get; private set; }
        public PreparationGraphic PlayerHealthBar { get; private set; }
        public PreparationGraphic EnemyHealthBar { get; private set; }
        private static readonly Color Pink = RunUI.Hex("FF397F"), Blue = RunUI.Hex("6C98FF"), Ice = RunUI.Hex("A0D9ED");

        internal void Bind(RunUI ui, RunSession session, Action weapons, Action practice, Action codex, Action menu,
            Action start, Action<MonsterDefinition> monsterCodex)
        {
            this.ui = ui; root = (RectTransform)transform;
            root.gameObject.AddComponent<RectMask2D>();
            ui.Background(root, RunUI.Ink);
            StageScenery.Add(ui, root, session.BattleMusic.Music, "Preparation scenery");

            HeroPortrait = Portrait("Preparation player", Resources.Load<Sprite>(ArtRoot + "player") ?? PlayerIdle(),
                new Vector2(-.035f, -.18f), new Vector2(.475f, .835f));
            var weaponsRoot = ui.Rect("Preparation equipped weapons", root); RunUI.Stretch(weaponsRoot);
            var weaponsArt = weaponsRoot.gameObject.AddComponent<WeaponBattleGraphic>();
            weaponsArt.WeaponSizeMultiplier = 2.25f;
            weaponsArt.SetFrame(new WeaponBattle(session.BattleLoadout, new StageHealth(session.EnemyHealth.Maximum),
                session.Health, session.MaxHealth, true), 0, new Vector2(.23f, -.08f), new Vector2(.23f, .35f));
            weaponsArt.Refresh();

            int count = session.BattlePlan.Monsters.Count;
            for (int i = 0; i < count; i++)
            {
                var monster = session.BattlePlan.Monsters[i].Monster;
                var sprite = Resources.Load<Sprite>(ArtRoot + "Monsters/" + monster.Id) ??
                    Resources.Load<Sprite>(MonsterAttackDefinition.ResourceRoot + monster.Id + "/idle") ??
                    Resources.Load<Sprite>("BBSB/BattleArt/" + monster.ArtId);
                float step = count == 1 ? 0 : .41f / count;
                var min = count == 1 ? new Vector2(.55f, -.20f) : new Vector2(.57f + i * step, -.08f);
                var max = count == 1 ? new Vector2(1.055f, .83f) : new Vector2(.57f + i * step + step * 1.40f, .77f - (i % 2) * .055f);
                portraits.Add(Portrait("Preparation monster " + session.BattlePlan.Monsters[i].InstanceId, sprite, min, max));
            }
            var shade = Graphic("Preparation lighting", root, PreparationGraphicKind.Atmosphere, Pink); RunUI.Stretch(shade.rectTransform);

            DrawHeader(session, weapons, practice, codex, menu);
            var versus = Graphic("Versus brush", root, PreparationGraphicKind.Versus, Pink);
            RunUI.Pin(versus.rectTransform, new Vector2(.5f, .49f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(290, 236));
            var v = Label(versus.transform, "V", 114, Pink, new Vector2(.09f, .10f), new Vector2(.58f, .93f));
            var s = Label(versus.transform, "S", 114, Color.white, new Vector2(.42f, .10f), new Vector2(.91f, .93f));
            v.fontStyle = s.fontStyle = FontStyle.BoldAndItalic;
            Shadow(v, RunUI.Hex("621835"), new Vector2(4, -5)); Shadow(s, RunUI.Hex("713AD2"), new Vector2(4, -5));

            StartButton = Button(root, "연주 시작", "START BATTLE", PreparationIcon.Play, RunUI.Gold, start, true);
            RunUI.Pin((RectTransform)StartButton.transform, new Vector2(.5f, .23f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(352, 100));
            DrawNames(session, monsterCodex);
        }

        private void DrawHeader(RunSession session, Action weapons, Action practice, Action codex, Action menu)
        {
            var setup = Button(root, "무기 배치", "WEAPON SETUP", PreparationIcon.Weapons, RunUI.Gold, weapons);
            var rehearse = Button(root, "연습 모드", "PRACTICE", PreparationIcon.Practice, Blue, practice);
            var book = Button(root, "몬스터 도감", "BESTIARY", PreparationIcon.Book, Ice, codex);
            var options = Button(root, "메뉴", "MENU", PreparationIcon.Menu, Ice, menu);
            Top((RectTransform)setup.transform, .012f, .171f, 14, 54);
            Top((RectTransform)rehearse.transform, .188f, .321f, 14, 54);
            Top((RectTransform)book.transform, .699f, .874f, 14, 54);
            Top((RectTransform)options.transform, .892f, .988f, 14, 54);

            var music = session.BattleMusic.Music;
            var stage = Label(root, "STAGE " + (session.CurrentNode.Row + 1).ToString("00"), 23, Pink, Vector2.zero, Vector2.one);
            Top(stage.rectTransform, .345f, .655f, 18, 28); stage.fontStyle = FontStyle.BoldAndItalic;
            var song = Label(root, music.Name, 54, Color.white, Vector2.zero, Vector2.one);
            Top(song.rectTransform, .325f, .675f, 43, 63); Fit(song, 30); song.fontStyle = FontStyle.BoldAndItalic; Shadow(song, RunUI.Ink, new Vector2(2, -3));
            var bpm = Label(root, music.Bpm.ToString("0.##") + " BPM · " + (StageCatalog.Find(music.Id)?.Meter ?? "4/4"), 31, Color.white, Vector2.zero, Vector2.one);
            Top(bpm.rectTransform, .37f, .63f, 106, 37); bpm.fontStyle = FontStyle.BoldAndItalic;
            var caption = Label(root, session.Map.Theme.Genre + "  ·  " + music.DurationSeconds.ToString("0") + "초", 10, RunUI.Muted, Vector2.zero, Vector2.one);
            Top(caption.rectTransform, .34f, .66f, 151, 18);
            PlayerHealthBar = Health("Preparation player HP", session.Health, session.MaxHealth, RunUI.Hex("00D9A5"), .012f, .310f, false);
            EnemyHealthBar = Health("Preparation shared enemy HP", session.EnemyHealth.Current, session.EnemyHealth.Maximum, Pink, .698f, .988f, true);
        }

        private PreparationGraphic Health(string name, decimal current, decimal maximum, Color tint, float left, float right, bool alignRight)
        {
            var label = Label(root, "HP  " + current.ToString("0.##") + " / " + maximum.ToString("0.##"), 20, Color.white, Vector2.zero, Vector2.one);
            label.name = name + " text"; label.alignment = alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            Top(label.rectTransform, left + .005f, right - .005f, 82, 24);
            var bar = Graphic(name, root, PreparationGraphicKind.Health, tint, value: maximum > 0 ? (float)(current / maximum) : 0);
            Top(bar.rectTransform, left, right, 108, 27); return bar;
        }

        private void DrawNames(RunSession session, Action<MonsterDefinition> open)
        {
            var crest = Graphic("Weapon master crest", root, PreparationGraphicKind.Icon, Color.white, PreparationIcon.Crest);
            RunUI.Pin(crest.rectTransform, new Vector2(.025f, .07f), Vector2.zero, Vector2.zero, new Vector2(42, 55));
            var name = Label(root, "WEAPON MASTER", 26, Color.white, new Vector2(.078f, .107f), new Vector2(.38f, .153f));
            name.alignment = TextAnchor.MiddleLeft; name.fontStyle = FontStyle.BoldAndItalic; Fit(name, 20);
            var title = Label(root, "이기어의 계승자", 17, RunUI.TextColor, new Vector2(.078f, .071f), new Vector2(.38f, .106f));
            title.alignment = TextAnchor.MiddleLeft;
            int count = session.BattlePlan.Monsters.Count;
            for (int i = 0; i < count; i++)
            {
                var monster = session.BattlePlan.Monsters[i].Monster;
                float left = .59f + i * .398f / count, right = .59f + (i + 1) * .398f / count;
                var names = ui.Rect("Preparation name " + monster.Id, root);
                RunUI.Overlay(names, new Vector2(left, .069f), new Vector2(right, .153f), new Vector2(4, 0), new Vector2(-4, 0));
                var lookup = Button(names, "도감", "", PreparationIcon.Search, Blue, () => open(monster));
                lookup.name = "Preparation codex " + monster.Id;
                RunUI.Pin((RectTransform)lookup.transform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(44, 52));
                var text = Label(names, monster.Name, count == 1 ? 25 : 20, Color.white, Vector2.zero, Vector2.one);
                RunUI.Overlay(text.rectTransform, new Vector2(0, .40f), Vector2.one, Vector2.zero, new Vector2(-52, 0));
                text.alignment = TextAnchor.MiddleRight; text.fontStyle = FontStyle.Bold; Fit(text, count == 3 ? 13 : 15);
                var epithet = Label(names, Epithet(monster.Id), count == 1 ? 16 : 13, RunUI.TextColor, Vector2.zero, Vector2.one);
                RunUI.Overlay(epithet.rectTransform, Vector2.zero, new Vector2(1, .40f), Vector2.zero, new Vector2(-52, 0));
                epithet.alignment = TextAnchor.MiddleRight; Fit(epithet, 10);
            }
        }

        private Button Button(Transform parent, string label, string english, PreparationIcon icon, Color tint, Action click, bool start = false)
        {
            var graphic = Graphic("Button " + label, parent, start ? PreparationGraphicKind.Start : PreparationGraphicKind.Glass, tint);
            graphic.raycastTarget = true;
            var button = graphic.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1);
            colors.pressedColor = new Color(.72f, .72f, .72f, 1); colors.fadeDuration = .08f; button.colors = colors;
            button.onClick.AddListener(() => click());
            bool compact = icon == PreparationIcon.Search;
            var text = Label(graphic.transform, label, start ? 32 : compact ? 10 : 22, start ? RunUI.Ink : Color.white,
                compact ? new Vector2(.05f, .05f) : new Vector2(.28f, .34f), compact ? new Vector2(.95f, .32f) : new Vector2(.95f, .88f));
            text.fontStyle = FontStyle.Bold; Fit(text, start ? 25 : compact ? 10 : 16);
            var symbol = Graphic("Button icon", graphic.transform, PreparationGraphicKind.Icon, start ? RunUI.Hex("563410") : Color.white, icon);
            RunUI.Overlay(symbol.rectTransform, compact ? new Vector2(.16f, .30f) : new Vector2(.065f, .21f),
                compact ? new Vector2(.84f, .91f) : new Vector2(.245f, .81f), Vector2.zero, Vector2.zero);
            if (!string.IsNullOrEmpty(english)) Label(graphic.transform, english, start ? 11 : 8, start ? RunUI.Hex("614226") : Color.Lerp(tint, Color.white, .35f),
                new Vector2(.28f, .13f), new Vector2(.94f, .34f));
            return button;
        }

        private PreparationGraphic Graphic(string name, Transform parent, PreparationGraphicKind kind, Color tint,
            PreparationIcon icon = PreparationIcon.None, float value = 1)
        {
            var rect = ui.Rect(name, parent); var graphic = rect.gameObject.AddComponent<PreparationGraphic>();
            graphic.Configure(kind, tint, icon, value); return graphic;
        }
        private Image Portrait(string name, Sprite sprite, Vector2 min, Vector2 max)
        {
            var rect = ui.Rect(name, root); RunUI.Overlay(rect, min, max, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true;
            image.raycastTarget = false; image.enabled = sprite != null; return image;
        }
        private Text Label(Transform parent, string text, int size, Color tint, Vector2 min, Vector2 max)
        {
            var label = ui.Label(parent, text, size, tint, 40, TextAnchor.MiddleCenter);
            RunUI.Overlay(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label;
        }
        private static void Top(RectTransform rect, float left, float right, float top, float height) =>
            RunUI.Overlay(rect, new Vector2(left, 1), new Vector2(right, 1), new Vector2(0, -top - height), new Vector2(0, -top));
        private static void Fit(Text label, int min)
        { label.resizeTextForBestFit = true; label.resizeTextMinSize = min; label.resizeTextMaxSize = label.fontSize; }
        private static void Shadow(Text label, Color tint, Vector2 offset)
        { var shadow = label.gameObject.AddComponent<Shadow>(); shadow.effectColor = tint; shadow.effectDistance = offset; }
        private static Sprite PlayerIdle()
        {
            foreach (var sprite in Resources.LoadAll<Sprite>(PlayerMotionSprites.ResourcePath + "idle"))
                if (sprite.name == "idle_0") return sprite;
            return Resources.Load<Sprite>("BBSB/BattleArt/weapon-master");
        }
        private static string Epithet(string id)
        {
            switch (id)
            {
                case "tap-slime": return "젤리 박자의 주인";
                case "march-slime": return "행진하는 도자기 기사";
                case "tresillo-bat": return "세 갈래 선율의 무희";
                case "offbeat-goblin": return "뒷박의 불꽃술사";
                case "drowsy-slime": return "쉼표를 삼키는 꿈";
                case "clock-spirit": return "일곱 걸음의 지휘자";
                case "seesaw-goblin": return "엇박을 당기는 두 꼬리";
                case "spark-bat": return "번개의 연주자";
                case "iron-turtle": return "흔들리지 않는 갑각";
                case "diving-ray": return "박자를 덮는 장막";
                case "bubble-spirit": return "공명의 우산";
                case "flick-goblin": return "거미줄의 지배자";
                default: return "";
            }
        }
    }
}
