using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public sealed class BattleHudBindings : MonoBehaviour
    {
        public RectTransform arena;
        public Text song, health, enemyHealth, combo, feedback, damage;
        public RectTransform songFill, healthFill, enemyFill;
        public Button menu;
        public bool IsValid => arena != null && song != null && health != null && enemyHealth != null && combo != null &&
            feedback != null && damage != null && songFill != null && healthFill != null && enemyFill != null && menu != null && healthFill.GetComponent<Image>() != null;
    }
}
