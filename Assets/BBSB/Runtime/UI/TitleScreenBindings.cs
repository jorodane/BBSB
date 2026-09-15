using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Runtime.UI
{
    public sealed class TitleScreenBindings : MonoBehaviour
    {
        public TextMeshProUGUI mapName;
        public Button start, codex, previousMap, nextMap;
        public bool IsValid => mapName != null && start != null && codex != null && previousMap != null && nextMap != null;
    }
}
