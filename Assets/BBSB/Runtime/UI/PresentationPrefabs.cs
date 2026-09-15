using System;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public enum RunScreenKind { Title, Map, Preparation, Battle, Report, Reward, Replacement, Rest, Upgrade, Shop, FieldCleared, GameOver, Codex }
    [CreateAssetMenu(menuName = "BBSB/Presentation Prefabs")]
    public sealed class PresentationPrefabs : ScriptableObject
    {
        public const string ResourcePath = "BBSB/Presentation/PresentationPrefabs";
        [Serializable] public sealed class Screen { public RunScreenKind kind; public CanvasScreen prefab; }
        public Screen[] screens = Array.Empty<Screen>();
        public Button primaryButton, secondaryButton;
        public Text titleText, headingText, bodyText, captionText;
        public RectTransform card;
        public CanvasScreen Create(RunScreenKind kind, Transform parent)
        {
            foreach (var entry in screens)
                if (entry != null && entry.kind == kind && entry.prefab != null)
                {
                    var result = Instantiate(entry.prefab, parent, false);
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
