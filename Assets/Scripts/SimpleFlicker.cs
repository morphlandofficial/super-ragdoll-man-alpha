using UnityEngine;

public class SimpleFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField] private int numberOfFlickerValues = 5;
    [SerializeField] private float flickerSpeed = 2.0f;
    [SerializeField] private float minIntensityRatio = 0.3f; // Minimum intensity as a ratio of original
    
    private Light lightComponent;
    private float timer;
    private int currentIndex;
    private float originalIntensity;
    private float[] flickerValues;
    
    void Start()
    {
        lightComponent = GetComponent<Light>();
        if (lightComponent == null)
        {
// Debug.LogError("SimpleFlicker: No Light component found!");
            enabled = false;
            return;
        }
        
        // Store the original intensity
        originalIntensity = lightComponent.intensity;
        
        // Generate flicker values relative to the original intensity
        GenerateFlickerValues();
        
        // Start with a random flicker value
        currentIndex = Random.Range(0, flickerValues.Length);
        lightComponent.intensity = flickerValues[currentIndex];
    }
    
    void Update()
    {
        timer += Time.deltaTime * flickerSpeed;
        
        if (timer >= 1.0f)
        {
            timer = 0f;
            
            // Pick a random flicker value
            currentIndex = Random.Range(0, flickerValues.Length);
            lightComponent.intensity = flickerValues[currentIndex];
        }
    }
    
    private void GenerateFlickerValues()
    {
        flickerValues = new float[numberOfFlickerValues];
        
        // Calculate minimum intensity based on ratio
        float minIntensity = originalIntensity * minIntensityRatio;
        
        // Generate random values between min and original intensity
        for (int i = 0; i < numberOfFlickerValues - 1; i++)
        {
            flickerValues[i] = Random.Range(minIntensity, originalIntensity);
        }
        
        // Ensure at least one value equals the original intensity
        flickerValues[numberOfFlickerValues - 1] = originalIntensity;
        
        // Shuffle the array to randomize the position of the max value
        for (int i = 0; i < flickerValues.Length; i++)
        {
            float temp = flickerValues[i];
            int randomIndex = Random.Range(i, flickerValues.Length);
            flickerValues[i] = flickerValues[randomIndex];
            flickerValues[randomIndex] = temp;
        }
    }
}