using UnityEngine;
using System.Collections.Generic;

public class ChildObjectCycler : MonoBehaviour
{
    [Header("Cycling Settings")]
    [SerializeField] private float cycleSpeed = 1.0f;
    [SerializeField] private bool startCyclingOnAwake = true;
    [SerializeField] private bool loopCycle = true;
    
    [Header("Debug Info")]
    [SerializeField] private int currentActiveIndex = 0;
    [SerializeField] private List<GameObject> childObjects = new List<GameObject>();
    
    private float timer = 0f;
    private bool isCycling = false;
    
    void Awake()
    {
        RefreshChildList();
        
        if (startCyclingOnAwake)
        {
            StartCycling();
        }
    }
    
    void Start()
    {
        // Ensure only the first child is active at start
        ActivateChildAtIndex(0);
    }
    
    void Update()
    {
        if (isCycling && childObjects.Count > 1)
        {
            timer += Time.deltaTime;
            
            if (timer >= cycleSpeed)
            {
                CycleToNext();
                timer = 0f;
            }
        }
    }
    
    /// <summary>
    /// Refreshes the list of child objects
    /// </summary>
    public void RefreshChildList()
    {
        childObjects.Clear();
        
        for (int i = 0; i < transform.childCount; i++)
        {
            childObjects.Add(transform.GetChild(i).gameObject);
        }
        
        // Clamp current index to valid range
        if (currentActiveIndex >= childObjects.Count)
        {
            currentActiveIndex = 0;
        }
    }
    
    /// <summary>
    /// Starts the cycling process
    /// </summary>
    public void StartCycling()
    {
        isCycling = true;
        timer = 0f;
    }
    
    /// <summary>
    /// Stops the cycling process
    /// </summary>
    public void StopCycling()
    {
        isCycling = false;
    }
    
    /// <summary>
    /// Cycles to the next child object
    /// </summary>
    public void CycleToNext()
    {
        if (childObjects.Count == 0) return;
        
        currentActiveIndex++;
        
        if (currentActiveIndex >= childObjects.Count)
        {
            if (loopCycle)
            {
                currentActiveIndex = 0;
            }
            else
            {
                currentActiveIndex = childObjects.Count - 1;
                StopCycling();
                return;
            }
        }
        
        ActivateChildAtIndex(currentActiveIndex);
    }
    
    /// <summary>
    /// Cycles to the previous child object
    /// </summary>
    public void CycleToPrevious()
    {
        if (childObjects.Count == 0) return;
        
        currentActiveIndex--;
        
        if (currentActiveIndex < 0)
        {
            if (loopCycle)
            {
                currentActiveIndex = childObjects.Count - 1;
            }
            else
            {
                currentActiveIndex = 0;
                return;
            }
        }
        
        ActivateChildAtIndex(currentActiveIndex);
    }
    
    /// <summary>
    /// Activates the child at the specified index and deactivates all others
    /// </summary>
    /// <param name="index">Index of the child to activate</param>
    public void ActivateChildAtIndex(int index)
    {
        if (index < 0 || index >= childObjects.Count) return;
        
        // Deactivate all children
        for (int i = 0; i < childObjects.Count; i++)
        {
            if (childObjects[i] != null)
            {
                childObjects[i].SetActive(false);
            }
        }
        
        // Activate the selected child
        if (childObjects[index] != null)
        {
            childObjects[index].SetActive(true);
            currentActiveIndex = index;
        }
    }
    
    /// <summary>
    /// Sets the cycle speed
    /// </summary>
    /// <param name="speed">Time in seconds between cycles</param>
    public void SetCycleSpeed(float speed)
    {
        cycleSpeed = Mathf.Max(0.1f, speed);
    }
    
    /// <summary>
    /// Gets the currently active child index
    /// </summary>
    /// <returns>Index of the currently active child</returns>
    public int GetCurrentActiveIndex()
    {
        return currentActiveIndex;
    }
    
    /// <summary>
    /// Gets the total number of child objects
    /// </summary>
    /// <returns>Number of child objects</returns>
    public int GetChildCount()
    {
        return childObjects.Count;
    }
    
    // Editor helper methods
    void OnValidate()
    {
        // Ensure cycle speed is never negative
        cycleSpeed = Mathf.Max(0.1f, cycleSpeed);
        
        // Refresh child list when values change in inspector
        if (Application.isPlaying)
        {
            RefreshChildList();
        }
    }
}