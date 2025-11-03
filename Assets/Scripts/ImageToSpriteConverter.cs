using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ImageToSpriteConverter : MonoBehaviour
{
    [Header("Image Settings")]
    [Tooltip("Drag and drop a PNG texture here")]
    public Texture2D imageTexture;

    [Header("Rendering Settings")]
    [Tooltip("Enable this to render the sprite on both sides")]
    public bool doubleSided = false;

    [Header("Sprite Settings")]
    [Tooltip("Pixels per unit for the sprite")]
    public float pixelsPerUnit = 100f;

    private Sprite currentSprite;
    private Material spriteMaterial;
    private SpriteRenderer spriteRenderer;
    private GameObject spriteChildObject;

    [ContextMenu("Convert Image to Sprite")]
    public void ConvertToSprite()
    {
        // Prevent execution during play mode
        if (Application.isPlaying)
        {
            Debug.LogWarning("ImageToSpriteConverter: Cannot create sprites during play mode. This is an editor-only tool.");
            return;
        }

        if (imageTexture == null)
        {
            Debug.LogWarning("No texture assigned to ImageToSpriteConverter!");
            return;
        }

        // Check if texture is readable, if not try to enable it
        if (!imageTexture.isReadable)
        {
#if UNITY_EDITOR
            if (!MakeTextureReadable(imageTexture))
            {
                Debug.LogError("Failed to make texture readable! Please manually enable Read/Write in the texture import settings.");
                return;
            }
#else
            Debug.LogError("Texture is not readable! Please enable Read/Write in the texture import settings.");
            return;
#endif
        }

        // Create or get the child GameObject
        if (spriteChildObject == null)
        {
            spriteChildObject = new GameObject("Sprite");
            spriteChildObject.transform.SetParent(transform);
            spriteChildObject.transform.localPosition = Vector3.zero;
            spriteChildObject.transform.localRotation = Quaternion.identity;
            spriteChildObject.transform.localScale = Vector3.one;
        }

        // Create sprite from texture
        Rect spriteRect = new Rect(0, 0, imageTexture.width, imageTexture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        currentSprite = Sprite.Create(imageTexture, spriteRect, pivot, pixelsPerUnit);

#if UNITY_EDITOR
        // Save the sprite as an asset so it persists in prefabs
        string texturePath = AssetDatabase.GetAssetPath(imageTexture);
        if (!string.IsNullOrEmpty(texturePath))
        {
            string directory = System.IO.Path.GetDirectoryName(texturePath);
            string spritePath = System.IO.Path.Combine(directory, imageTexture.name + "_Sprite.asset");
            
            // Delete old sprite asset if it exists
            Sprite existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (existingSprite != null)
            {
                AssetDatabase.DeleteAsset(spritePath);
            }
            
            AssetDatabase.CreateAsset(currentSprite, spritePath);
            AssetDatabase.SaveAssets();
            
            // Reload the sprite from disk so it's a proper asset reference
            currentSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }
#endif

        // Setup or get SpriteRenderer component on child
        spriteRenderer = spriteChildObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = spriteChildObject.AddComponent<SpriteRenderer>();
        }

        // Apply the sprite
        spriteRenderer.sprite = currentSprite;

        // Create material with transparency support
        CreateTransparentMaterial();

        // Apply double-sided rendering if needed
        ApplyRenderingMode();

        Debug.Log($"Successfully converted texture '{imageTexture.name}' to sprite!");
    }

    private void CreateTransparentMaterial()
    {
        // Create a material that supports transparency
        if (spriteMaterial == null)
        {
            // Use the default sprite shader which supports transparency
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                spriteMaterial = new Material(spriteShader);
            }
            else
            {
                // Fallback to standard shader with transparent mode
                Shader standardShader = Shader.Find("Standard");
                if (standardShader != null)
                {
                    spriteMaterial = new Material(standardShader);
                    spriteMaterial.SetFloat("_Mode", 3); // Transparent mode
                    spriteMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    spriteMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    spriteMaterial.SetInt("_ZWrite", 0);
                    spriteMaterial.DisableKeyword("_ALPHATEST_ON");
                    spriteMaterial.EnableKeyword("_ALPHABLEND_ON");
                    spriteMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    spriteMaterial.renderQueue = 3000;
                }
            }

#if UNITY_EDITOR
            // Save the material as an asset so it persists in prefabs
            if (imageTexture != null)
            {
                string texturePath = AssetDatabase.GetAssetPath(imageTexture);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    string directory = System.IO.Path.GetDirectoryName(texturePath);
                    string materialPath = System.IO.Path.Combine(directory, imageTexture.name + "_SpriteMaterial.mat");
                    
                    // Delete old material asset if it exists
                    Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (existingMaterial != null)
                    {
                        AssetDatabase.DeleteAsset(materialPath);
                    }
                    
                    AssetDatabase.CreateAsset(spriteMaterial, materialPath);
                    AssetDatabase.SaveAssets();
                    
                    // Reload the material from disk so it's a proper asset reference
                    spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                }
            }
#endif
        }

        if (spriteRenderer != null && spriteMaterial != null)
        {
            spriteRenderer.material = spriteMaterial;
        }
    }

    private void ApplyRenderingMode()
    {
        if (spriteRenderer == null) return;

        // For SpriteRenderer, we need to handle double-sided differently
        // SpriteRenderer is always front-facing, so for double-sided we need a mesh approach
        if (doubleSided)
        {
            // Remove SpriteRenderer and use MeshRenderer instead for true double-sided rendering
            ConvertToMeshRenderer();
        }
        else
        {
            // Keep using SpriteRenderer for single-sided rendering
            if (spriteMaterial != null)
            {
                spriteRenderer.material = spriteMaterial;
            }
        }
    }

    private void ConvertToMeshRenderer()
    {
        if (spriteChildObject == null) return;

        // Remove SpriteRenderer if it exists
        if (spriteRenderer != null)
        {
            if (Application.isPlaying)
                Destroy(spriteRenderer);
            else
                DestroyImmediate(spriteRenderer);
        }

        // Get or create MeshFilter and MeshRenderer on child object
        MeshFilter meshFilter = spriteChildObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = spriteChildObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = spriteChildObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = spriteChildObject.AddComponent<MeshRenderer>();
        }

        // Create a quad mesh
        Mesh mesh = CreateQuadMesh();
        
#if UNITY_EDITOR
        // Save the mesh as an asset so it persists in prefabs
        if (imageTexture != null)
        {
            string texturePath = AssetDatabase.GetAssetPath(imageTexture);
            if (!string.IsNullOrEmpty(texturePath))
            {
                string directory = System.IO.Path.GetDirectoryName(texturePath);
                string meshPath = System.IO.Path.Combine(directory, imageTexture.name + "_Mesh.asset");
                
                // Delete old mesh asset if it exists
                Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existingMesh != null)
                {
                    AssetDatabase.DeleteAsset(meshPath);
                }
                
                AssetDatabase.CreateAsset(mesh, meshPath);
                AssetDatabase.SaveAssets();
                
                // Reload the mesh from disk so it's a proper asset reference
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            }
        }
#endif
        
        meshFilter.mesh = mesh;

        // Create material with double-sided rendering
        Material doubleSidedMaterial = new Material(Shader.Find("Unlit/Transparent"));
        doubleSidedMaterial.mainTexture = imageTexture;
        doubleSidedMaterial.SetInt("_Cull", 0); // Disable culling for double-sided rendering

#if UNITY_EDITOR
        // Save the material as an asset so it persists in prefabs
        if (imageTexture != null)
        {
            string texturePath = AssetDatabase.GetAssetPath(imageTexture);
            if (!string.IsNullOrEmpty(texturePath))
            {
                string directory = System.IO.Path.GetDirectoryName(texturePath);
                string materialPath = System.IO.Path.Combine(directory, imageTexture.name + "_DoubleSidedMaterial.mat");
                
                // Delete old material asset if it exists
                Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (existingMaterial != null)
                {
                    AssetDatabase.DeleteAsset(materialPath);
                }
                
                AssetDatabase.CreateAsset(doubleSidedMaterial, materialPath);
                AssetDatabase.SaveAssets();
                
                // Reload the material from disk so it's a proper asset reference
                doubleSidedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
        }
#endif

        meshRenderer.material = doubleSidedMaterial;
    }

    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ImageQuad";

        float width = imageTexture.width / pixelsPerUnit;
        float height = imageTexture.height / pixelsPerUnit;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        // Vertices for a centered quad
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-halfWidth, -halfHeight, 0),
            new Vector3(halfWidth, -halfHeight, 0),
            new Vector3(-halfWidth, halfHeight, 0),
            new Vector3(halfWidth, halfHeight, 0)
        };

        // UVs
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        // Triangles (front and back faces)
        int[] triangles = new int[12]
        {
            // Front face
            0, 2, 1,
            2, 3, 1,
            // Back face (reversed winding for proper culling)
            1, 3, 0,
            3, 2, 0
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    [ContextMenu("Clear Sprite")]
    public void ClearSprite()
    {
        // Prevent execution during play mode
        if (Application.isPlaying)
        {
            Debug.LogWarning("ImageToSpriteConverter: Cannot clear sprites during play mode. This is an editor-only tool.");
            return;
        }

        if (spriteChildObject != null)
        {
            DestroyImmediate(spriteChildObject);
            spriteChildObject = null;
        }

        spriteRenderer = null;
        currentSprite = null;
        Debug.Log("Sprite cleared!");
    }

#if UNITY_EDITOR
    private bool MakeTextureReadable(Texture2D texture)
    {
        if (texture == null) return false;

        string assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("Could not find asset path for texture. Make sure the texture is imported as an asset.");
            return false;
        }

        TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (textureImporter == null)
        {
            Debug.LogWarning("Could not get TextureImporter for the texture.");
            return false;
        }

        if (!textureImporter.isReadable)
        {
            Debug.Log($"Enabling Read/Write for texture: {texture.name}");
            textureImporter.isReadable = true;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            return true;
        }

        return true;
    }
#endif
}

