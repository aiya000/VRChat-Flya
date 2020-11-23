﻿using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class FlyingByToggling : UdonSharpBehaviour {
    /// <summary>
    /// Increase this to speed up flying.
    /// </summary>
    [SerializeField]
    private float flappingStrength = 5.0f;

    /// <summary>
    /// The owner of Flyer by pickupping.
    ///
    /// Being null means this object is not picked up now.
    /// </summary>
    private VRCPlayerApi owner;

    /// <summary>
    /// Fly with the owner when this is true.
    /// </summary>
    private bool readyToFly = false;

    /// <summary>
    /// Initailizes this instance to fly.
    /// </summary>
    public override void OnPickup() {
        this.owner = Networking.LocalPlayer;
        this.readyToFly = false;
    }

    /// <summary>
    /// Initializes this instance to stop.
    /// </summary>
    public override void OnDrop() {
        this.owner = null;
        this.readyToFly = false;
    }

    /// <summary>
    /// Start to fly.
    /// </summary>
    public override void OnPickupUseDown() {
        this.readyToFly = !this.readyToFly;
    }

    /// <summary>
    /// Emulates flying the sky :3
    /// </summary>
    public void FixedUpdate() {
        if (this.owner == null || !this.readyToFly) {
            return;
        }

        this.AffectOnceToFly();
    }

    /// <summary>
    /// Fix the owner's position to upper.
    ///
    /// Requirement:
    /// - this.owner != null
    /// </summary>
    private void AffectOnceToFly() {
        var movingUp = Vector3.Lerp(
                this.owner.GetVelocity(),
                Vector3.up * this.flappingStrength,
                4 * Time.fixedDeltaTime
                );
        this.owner.SetVelocity(movingUp);
    }
}
