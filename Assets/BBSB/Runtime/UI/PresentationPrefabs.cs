using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public enum RunScreenKind { Title, Map, Preparation, Battle, Report, Reward, Replacement, Rest, Upgrade, Shop, FieldCleared, GameOver, Codex, FiveLaneBattle, FiveLanePreparation }
    [CreateAssetMenu(menuName = "BBSB/Presentation Prefabs")]
    public sealed class PresentationPrefabs : ScriptableObject
    {
        public const string ResourcePath = "BBSB/Presentation/PresentationPrefabs";
        [Serializable] public sealed class Screen { public RunScreenKind kind; public CanvasScreen prefab; }
        public Screen[] screens = Array.Empty<Screen>();
        public Button primaryButton, secondaryButton;
        public TextMeshProUGUI titleText, headingText, bodyText, captionText;
        public RectTransform card;
        [Tooltip("기본 TMP 폰트. 개별 텍스트 프리팹의 폰트와 머티리얼은 그대로 유지해.")]
        public TMP_FontAsset defaultFont;
        [HideInInspector] public int textMeshProVersion;

        public void PrepareText(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.font == null || (textMeshProVersion < 1 && PresentationFonts.NeedsProjectFont(text)))
                    text.font = defaultFont != null ? defaultFont : PresentationFonts.Load();
        }
        public CanvasScreen Create(RunScreenKind kind, Transform parent)
        {
            foreach (var entry in screens)
                if (entry != null && entry.kind == kind && entry.prefab != null)
                {
                    var result = Instantiate(entry.prefab, parent, false);
                    PrepareText(result.gameObject);
                    var root = (RectTransform)result.transform;
                    root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                    root.offsetMin = root.offsetMax = Vector2.zero;
                    if (result.content == null) throw new InvalidOperationException(kind + " 화면의 Content 연결이 비어 있어.");
                    return result;
                }
            return null;
        }
    }
}
