using UnityEngine;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastSize;
        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            Rect area = Screen.safeArea;
            if (size.x <= 0 || size.y <= 0 || (size == lastSize && area == lastArea)) return;
            lastSize = size; lastArea = area;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            rect.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
