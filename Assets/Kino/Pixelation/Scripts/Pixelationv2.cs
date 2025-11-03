using UnityEngine;

namespace Assets.Pixelation.Scripts
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public sealed class Pixelationv2 : MonoBehaviour
    {
        [Range(64.0f, 2048.0f)]
        public float BlockCount = 128;

        private Material material;
        
        void OnEnable()
        {
            UnityEngine.Debug.Log($"<color=orange>[Pixelationv2]</color> OnEnable - Camera: {GetComponent<Camera>()?.name}, BlockCount: {BlockCount}");
        }
        
        void OnDisable()
        {
            UnityEngine.Debug.Log($"<color=orange>[Pixelationv2]</color> OnDisable - Camera: {GetComponent<Camera>()?.name}, BlockCount: {BlockCount}");
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                material = new Material(Shader.Find("Hidden/Custom/Pixelation"));
            }

            float k = (float)source.width / source.height;
            var count = new Vector2(BlockCount, BlockCount / k);
            var size = new Vector2(1.0f / count.x, 1.0f / count.y);

            material.SetVector("BlockCount", count);
            material.SetVector("BlockSize", size);

            Graphics.Blit(source, destination, material);
        }

        void OnDestroy()
        {
            if (material != null)
            {
                DestroyImmediate(material);
            }
        }
    }
}
