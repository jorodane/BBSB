using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public sealed class PreparationScreenBindings : MonoBehaviour
    {
        public Image portrait;
        public TextMeshProUGUI playerName, song, playerHealth, enemyHealth;
        public RectTransform playerFill, enemyFill, weapons;
        public ScrollRect patterns;
        public Button start, menu, codex;
        public bool IsValid => portrait != null && playerName != null && song != null && playerHealth != null && enemyHealth != null &&
            playerFill != null && enemyFill != null && weapons != null && patterns != null && patterns.content != null && patterns.viewport != null &&
            start != null && menu != null && codex != null;
    }
}
