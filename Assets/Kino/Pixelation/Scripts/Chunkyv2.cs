using UnityEngine;

namespace Assets.Pixelation.Scripts
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public sealed class Chunkyv2 : MonoBehaviour
    {
        public Texture2D SprTex = null;
        public Color Color = Color.white;

        private Material material;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (SprTex == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (material == null)
            {
                material = new Material(Shader.Find("Hidden/Custom/Chunky"));
            }

            var textDimsOrDefault = new Vector2(SprTex.width, SprTex.height);
            var w = source.width;
            var h = source.height;
            var count = new Vector2(w / textDimsOrDefault.x, h / textDimsOrDefault.y);
            var size = new Vector2(1.0f / count.x, 1.0f / count.y);

            material.SetVector("BlockCount", count);
            material.SetVector("BlockSize", size);
            material.SetColor("_Color", Color);
            material.SetTexture("_SprTex", SprTex);

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


