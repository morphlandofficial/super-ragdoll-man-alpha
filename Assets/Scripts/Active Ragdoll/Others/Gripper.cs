using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ActiveRagdoll {
    // Author: Sergio Abreu García | https://sergioabreu.me

    public class Gripper : MonoBehaviour {
        public GripModule GripMod { get; set; }

        /// <summary> If the component is activated after colliding with something, it won't grip
        /// to it unless the collision enters again. This variable hold the last collision to avoid
        /// skipping it. </summary>
        private Rigidbody _lastCollision;

        private ConfigurableJoint _joint;
        private Grippable _gripped;
        
        public void Start() {
            // Start disabled is useful to avoid fake gripping something at the start
            enabled = false;
        }

        private void Grip(Rigidbody whatToGrip) {
            if (!enabled) {
                _lastCollision = whatToGrip;
                return;
            }

            if (_joint != null)
                return;

            if (!GripMod.canGripYourself
                    && whatToGrip.transform.IsChildOf(GripMod.ActiveRagdoll.transform))
                return;

            // NEW: AI ragdolls can only grab players, not other AI ragdolls
            // Check if this gripper belongs to an AI ragdoll
            RagdollAIController thisAI = GripMod.ActiveRagdoll.GetComponent<RagdollAIController>();
            if (thisAI != null) {
                // This is an AI ragdoll trying to grab something
                // Check if the target is also an AI ragdoll (check both ways for safety)
                RagdollAIController targetAI = whatToGrip.GetComponentInParent<RagdollAIController>();
                if (targetAI == null) {
                    targetAI = whatToGrip.GetComponentInChildren<RagdollAIController>();
                }
                if (targetAI == null) {
                    targetAI = whatToGrip.GetComponent<RagdollAIController>();
                }
                
                if (targetAI != null) {
                    // Target is another AI ragdoll - don't allow grabbing
                    return;
                }
            }

            _joint = gameObject.AddComponent<ConfigurableJoint>();
            _joint.connectedBody = whatToGrip;
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;

            // Try to find Grippable on the rigidbody itself
            if (whatToGrip.TryGetComponent(out _gripped)) {
                _gripped.jointMotionsConfig.ApplyTo(ref _joint);
                // Notify the grippable object that it's being held
                _gripped.NotifyGripped();
            }
            // If not found, search up the hierarchy (for ragdolls with Grippable on root)
            else {
                _gripped = whatToGrip.GetComponentInParent<Grippable>();
                
                if (_gripped != null) {
                    _gripped.jointMotionsConfig.ApplyTo(ref _joint);
                    _gripped.NotifyGripped();
                }
                else {
                    GripMod.defaultMotionsConfig.ApplyTo(ref _joint);
                }
            }
        }

        private void UnGrip() {
            if (_joint == null)
                return;

            // Notify the grippable object that it's being released
            if (_gripped != null) {
                _gripped.NotifyReleased();
            }

            Destroy(_joint);
            _joint = null;
            _gripped = null;
        }



        private void OnCollisionEnter(Collision collision) {
            if (GripMod.onlyUseTriggers)
                return;

            if (collision.rigidbody != null)
                Grip(collision.rigidbody);
        }

        private void OnTriggerEnter(Collider other) {
            if (other.attachedRigidbody != null)
                Grip(other.attachedRigidbody);
        }

        private void OnCollisionExit(Collision collision) {
            if (collision.rigidbody == _lastCollision)
                _lastCollision = null;
        }

        private void OnTriggerExit(Collider other) {
            if (other.attachedRigidbody == _lastCollision)
                _lastCollision = null;
        }



        private void OnEnable() {
            if (_lastCollision != null)
                Grip(_lastCollision);
        }

        private void OnDisable() {
            UnGrip();
        }
    }
} // namespace ActiveRagdoll
