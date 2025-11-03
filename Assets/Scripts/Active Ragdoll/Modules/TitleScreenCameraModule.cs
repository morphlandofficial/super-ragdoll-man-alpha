using UnityEngine;
using UnityEngine.InputSystem;

namespace ActiveRagdoll {
    /// <summary>
    /// Title Screen version of CameraModule that disables camera positioning and input.
    /// Use this on the title screen character when you have a separate fixed camera.
    /// This prevents the module from trying to move/rotate a camera and blocks input processing.
    /// </summary>
    public class TitleScreenCameraModule : CameraModule {
        
        [Header("--- TITLE SCREEN SETTINGS ---")]
        [Tooltip("When enabled, completely disables camera positioning/input. Use when you have a fixed external camera.")]
        [SerializeField] private bool disableCameraSystem = true;
        
        // Override Update to prevent camera positioning when disabled
        protected override void Update() {
            if (!disableCameraSystem) {
                // If camera system is enabled, call the base implementation
                base.Update();
            }
            // Otherwise do nothing - camera positioning/input is disabled
        }
        
        // Override the OnLook input handler to do nothing when disabled
        public new void OnLook(InputValue value) {
            if (!disableCameraSystem) {
                // If camera system is enabled, call the base implementation
                base.OnLook(value);
            }
            // Otherwise do nothing - ignore camera input
        }
        
        // Override the OnScrollWheel input handler to do nothing when disabled
        public new void OnScrollWheel(InputValue value) {
            if (!disableCameraSystem) {
                base.OnScrollWheel(value);
            }
            // Otherwise do nothing - ignore scroll input
        }
        
        // Override Start to prevent camera creation when disabled
        protected override void Start() {
            if (!disableCameraSystem) {
                base.Start();
            } else {
                
                // Just find the main camera for reference (for effects like datamosh)
                UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                if (mainCam != null) {
                    Camera = mainCam.gameObject;
                } else {
// Debug.LogWarning("[TitleScreenCameraModule] No main camera found! Make sure your fixed camera is tagged as MainCamera");
                }
            }
        }
        
        // Add this to verify it's actually being used
        private void Awake() {
        }
    }
}

