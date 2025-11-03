using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for EarthCarouselController that allows cycling through planets in Edit mode.
/// Adds buttons to the inspector for easy planet navigation while editing.
/// </summary>
[CustomEditor(typeof(EarthCarouselController))]
public class EarthCarouselControllerEditor : Editor
{
    private int currentPlanetIndex = 0;
    private Transform[] planetChildren;
    private Vector3 originalParentPosition;
    private bool isInitialized = false;
    
    private void OnEnable()
    {
        InitializePlanets();
    }
    
    private void InitializePlanets()
    {
        EarthCarouselController controller = (EarthCarouselController)target;
        Transform parent = controller.transform;
        
        int childCount = parent.childCount;
        if (childCount == 0)
        {
            isInitialized = false;
            return;
        }
        
        // Store all children in order
        planetChildren = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            planetChildren[i] = parent.GetChild(i);
        }
        
        // Store the original parent position (where planet 0 is centered)
        originalParentPosition = parent.position;
        
        isInitialized = true;
    }
    
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        EarthCarouselController controller = (EarthCarouselController)target;
        
        // Always try to initialize if not ready
        if (!isInitialized || planetChildren == null || planetChildren.Length == 0)
        {
            InitializePlanets();
        }
        
        // Check if we have planets
        int childCount = controller.transform.childCount;
        
        if (childCount == 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("No planet children found! Add child objects to the PLANETS parent.", MessageType.Warning);
            return;
        }
        
        if (planetChildren == null || planetChildren.Length == 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox($"Detected {childCount} children but failed to initialize. Try clicking 'Reset to First Planet' below.", MessageType.Warning);
            if (GUILayout.Button("Force Reinitialize"))
            {
                InitializePlanets();
                Repaint();
            }
            return;
        }
        
        // Add spacing
        EditorGUILayout.Space(10);
        
        // Header
        EditorGUILayout.LabelField("Edit Mode Planet Navigation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"Currently viewing: Planet {currentPlanetIndex + 1}/{planetChildren.Length} ({planetChildren[currentPlanetIndex].name})", MessageType.Info);
        
        // Navigation buttons
        EditorGUILayout.BeginHorizontal();
        
        // Previous button
        if (GUILayout.Button("◄ Previous Planet", GUILayout.Height(30)))
        {
            CycleToPreviousPlanet(controller);
        }
        
        // Next button
        if (GUILayout.Button("Next Planet ►", GUILayout.Height(30)))
        {
            CycleToNextPlanet(controller);
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Reset to first planet button
        if (GUILayout.Button("Reset to First Planet", GUILayout.Height(25)))
        {
            GoToPlanet(controller, 0);
        }
        
        EditorGUILayout.Space(5);
        
        // Direct planet selection dropdown
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Jump to Planet:", GUILayout.Width(100));
        
        int selectedPlanet = EditorGUILayout.Popup(currentPlanetIndex, GetPlanetNames());
        if (selectedPlanet != currentPlanetIndex)
        {
            GoToPlanet(controller, selectedPlanet);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // Planet list with info
        EditorGUILayout.LabelField("Planet List:", EditorStyles.boldLabel);
        for (int i = 0; i < planetChildren.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            string prefix = (i == currentPlanetIndex) ? "➤ " : "   ";
            EditorGUILayout.LabelField($"{prefix}Planet {i + 1}: {planetChildren[i].name}");
            
            if (GUILayout.Button("View", GUILayout.Width(50)))
            {
                GoToPlanet(controller, i);
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void CycleToNextPlanet(EarthCarouselController controller)
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return;
        
        // Move to next planet (wrap around)
        currentPlanetIndex = (currentPlanetIndex + 1) % planetChildren.Length;
        
        // Move parent position
        MoveToPlanet(controller, currentPlanetIndex);
        
        Debug.Log($"[Editor] Moved to planet {currentPlanetIndex + 1}: {planetChildren[currentPlanetIndex].name}");
    }
    
    private void CycleToPreviousPlanet(EarthCarouselController controller)
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return;
        
        // Move to previous planet (wrap around)
        currentPlanetIndex--;
        if (currentPlanetIndex < 0)
            currentPlanetIndex = planetChildren.Length - 1;
        
        // Move parent position
        MoveToPlanet(controller, currentPlanetIndex);
        
        Debug.Log($"[Editor] Moved to planet {currentPlanetIndex + 1}: {planetChildren[currentPlanetIndex].name}");
    }
    
    private void GoToPlanet(EarthCarouselController controller, int planetIndex)
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return;
        
        if (planetIndex < 0 || planetIndex >= planetChildren.Length)
            return;
        
        currentPlanetIndex = planetIndex;
        
        // Move parent position
        MoveToPlanet(controller, currentPlanetIndex);
        
        Debug.Log($"[Editor] Jumped to planet {currentPlanetIndex + 1}: {planetChildren[currentPlanetIndex].name}");
    }
    
    private void MoveToPlanet(EarthCarouselController controller, int planetIndex)
    {
        if (controller == null || controller.transform == null)
        {
            Debug.LogError("[Editor] Controller or transform is null!");
            return;
        }
        
        if (planetChildren == null || planetIndex >= planetChildren.Length)
        {
            Debug.LogError($"[Editor] Invalid planet index {planetIndex} or planetChildren is null!");
            return;
        }
        
        // Get the invert direction setting from the serialized property
        SerializedProperty invertDirectionProp = serializedObject.FindProperty("invertDirection");
        bool invertDirection = invertDirectionProp != null && invertDirectionProp.boolValue;
        
        Debug.Log($"[Editor] Moving to planet {planetIndex + 1}. Invert Direction: {invertDirection}");
        
        // Calculate target position (same logic as runtime)
        Vector3 firstChildLocalPos = planetChildren[0].localPosition;
        Vector3 targetChildLocalPos = planetChildren[planetIndex].localPosition;
        float offsetFromFirst = targetChildLocalPos.x - firstChildLocalPos.x;
        
        Debug.Log($"[Editor] First child local X: {firstChildLocalPos.x}, Target child local X: {targetChildLocalPos.x}, Offset: {offsetFromFirst}");
        Debug.Log($"[Editor] Original parent position: {originalParentPosition}");
        
        // Set parent position
        Vector3 targetPosition = originalParentPosition;
        if (invertDirection)
            targetPosition.x = originalParentPosition.x + offsetFromFirst;
        else
            targetPosition.x = originalParentPosition.x - offsetFromFirst;
        
        Debug.Log($"[Editor] Target position: {targetPosition}, Current position: {controller.transform.position}");
        
        // Record undo so user can undo the position change
        Undo.RecordObject(controller.transform, $"Cycle to Planet {planetIndex + 1}");
        
        // Apply the position
        controller.transform.position = targetPosition;
        
        Debug.Log($"[Editor] Position applied. New position: {controller.transform.position}");
        
        // Mark the scene as dirty so changes are saved
        EditorUtility.SetDirty(controller.transform);
        
        // Update the scene view
        SceneView.RepaintAll();
    }
    
    private string[] GetPlanetNames()
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return new string[0];
        
        string[] names = new string[planetChildren.Length];
        for (int i = 0; i < planetChildren.Length; i++)
        {
            names[i] = $"Planet {i + 1}: {planetChildren[i].name}";
        }
        
        return names;
    }
    
    private void OnSceneGUI()
    {
        // Optional: Draw gizmos or handles in the scene view
        if (!isInitialized || planetChildren == null)
            return;
        
        // Highlight current planet in scene view
        if (currentPlanetIndex >= 0 && currentPlanetIndex < planetChildren.Length)
        {
            Transform currentPlanet = planetChildren[currentPlanetIndex];
            if (currentPlanet != null)
            {
                Handles.color = Color.green;
                Handles.DrawWireDisc(currentPlanet.position, Vector3.up, 30f);
                
                // Draw label
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.green;
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 14;
                Handles.Label(currentPlanet.position + Vector3.up * 35f, 
                    $"Current: Planet {currentPlanetIndex + 1}", style);
            }
        }
    }
}

