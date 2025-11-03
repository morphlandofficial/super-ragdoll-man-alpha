using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ActiveRagdoll {
    // Author: Sergio Abreu García | https://sergioabreu.me

    public class Grippable : MonoBehaviour {
        public JointMotionsConfig jointMotionsConfig;
        
        // Grip state tracking
        private bool _isBeingHeld = false;
        
        /// <summary>
        /// Event fired when this object is gripped by a hand
        /// </summary>
        public event Action OnGripped;
        
        /// <summary>
        /// Event fired when this object is released from a grip
        /// </summary>
        public event Action OnReleased;
        
        /// <summary>
        /// Returns true if this object is currently being held by a hand
        /// </summary>
        public bool IsBeingHeld => _isBeingHeld;
        
        /// <summary>
        /// Called by Gripper when this object is gripped
        /// </summary>
        public void NotifyGripped() {
            if (_isBeingHeld)
                return; // Already being held
                
            _isBeingHeld = true;
            OnGripped?.Invoke();
        }
        
        /// <summary>
        /// Called by Gripper when this object is released
        /// </summary>
        public void NotifyReleased() {
            if (!_isBeingHeld)
                return; // Already released
                
            _isBeingHeld = false;
            OnReleased?.Invoke();
        }
    }
} // namespace ActiveRagdoll