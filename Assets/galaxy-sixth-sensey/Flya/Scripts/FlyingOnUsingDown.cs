using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using VRC.Udon;

public class FlyingOnUsingDown : UdonSharpBehaviour {
    private readonly int FLYING_DISTANCE = 50;

    /// <summary>
    /// The owner of Flyer by pickupping.
    ///
    /// Being null means this object is not picked up now.
    /// </summary>
    private VRCPlayerApi owner;

    /// <summary>
    /// A time count to fix the owner's velocity.
    /// </summary>
    private int countToFly;

    /// <summary>
    /// Initailizes this instance to fly.
    /// </summary>
    public override void OnPickup() {
        this.owner = Networking.LocalPlayer;
        this.countToFly = 0;
    }

    /// <summary>
    /// Initializes this instance to stop.
    /// </summary>
    public override void OnDrop() {
        this.owner = null;
        this.countToFly = 0;
    }

    /// <summary>
    /// Start to fly.
    /// </summary>
    public override void OnPickupUseDown() {
        this.countToFly = FLYING_DISTANCE;
    }

    /// <summary>
    /// Emulates flying the sky :3
    /// </summary>
    public void FixedUpdate() {
        if (this.owner == null || this.countToFly == 0) {
            return;
        }

        this.AffectOnceToFly();
        this.countToFly--;
    }

    /// <summary>
    /// Fix the owner's position to upper.
    ///
    /// Requirement:
    /// - this.owner != null
    /// </summary>
    private void AffectOnceToFly() {
        var flying = new Vector3(0, this.transform.position.y - 0.1f, 0);
        var nextPosition = Vector3.Lerp(this.owner.GetVelocity(), flying, 4 * Time.fixedDeltaTime);
        this.owner.SetVelocity(nextPosition);
    }
}
