using UnityEngine;

public class SetGameObjectActive
{
    public static void SetActive()
    {
        // Set NPC Dialogue Panel inactive
        GameObject dialoguePanel = GameObject.Find("LEVEL SYSTEMS 2/UI & Points Systems/Canvas/NPC Dialogue Panel");
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // Set Interaction Prompt inactive
        GameObject interactionPrompt = GameObject.Find("LEVEL SYSTEMS 2/UI & Points Systems/Canvas/Interaction Prompt");
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }
}