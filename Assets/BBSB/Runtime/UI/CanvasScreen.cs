using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public sealed class CanvasScreen : MonoBehaviour
    {
        public RectTransform content;
        public Text heading, description;
        public TitleScreenBindings title;
        public BattleHudBindings battle;
        public PreparationScreenBindings preparation;
    }
}
