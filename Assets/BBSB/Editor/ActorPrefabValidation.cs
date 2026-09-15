using System;
using UnityEngine;
using UnityEngine.UI;
namespace BBSB.Editor
{
    internal static class ActorPrefabValidation
    {
        internal static void Validate(GameObject prefab)
        {
            if (!prefab.activeSelf) throw new ArgumentException("외형 프리팹 루트를 활성화해줘.");
            if (prefab.GetComponentsInChildren<Animator>(true).Length > 1) throw new ArgumentException("외형 프리팹에는 Animator 하나를 사용해줘.");
            var sprites = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            var images = prefab.GetComponentsInChildren<Image>(true);
            if (sprites.Length == 0 && (prefab.GetComponent<RectTransform>() == null || images.Length == 0))
                throw new ArgumentException("SpriteRenderer 또는 RectTransform + UI Image가 필요해.");
            if (sprites.Length > 0 && images.Length > 0) throw new ArgumentException("한 외형에서는 SpriteRenderer와 UI Image 중 한 방식을 사용해줘.");
            foreach (var sprite in sprites)
                if (sprite.drawMode != SpriteDrawMode.Simple) throw new ArgumentException("SpriteRenderer Draw Mode는 Simple로 설정해줘.");
        }
    }
}
