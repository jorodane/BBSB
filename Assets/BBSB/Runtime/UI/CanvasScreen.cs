using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public sealed class CanvasScreen : MonoBehaviour
    {
        public RectTransform content;
        public TextMeshProUGUI heading, description;
        public TitleScreenBindings title;
        public BattleHudBindings battle;
        public PreparationScreenBindings preparation;
        public FiveLaneHudBindings fiveLane;
        public RunSetupBindings runSetup;
    }
}
