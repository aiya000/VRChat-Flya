using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using VRC.Udon;

public class Flyer : UdonSharpBehaviour {
    private readonly int WAITING_COUNT = 50;

    /// <summary>
    /// The owner of Flyer by pickupping.
    ///
    /// Being null means this object is not picked up now.
    /// </summary>
    private VRCPlayerApi owner;

    /// <summary>
    /// A limitation for EmulateFlying() executions.
    /// </summary>
    private int countToSkip = 0;

    private Vector3 latestOwnerPos = Vector3.zero;

    /// <summary>
    /// Initailizes this instance to fly.
    /// </summary>
    public override void OnPickup() {
        this.owner = Networking.LocalPlayer;
        this.countToSkip = WAITING_COUNT;
        this.latestOwnerPos = this.transform.position;
    }

    /// <summary>
    /// Initializes this instance to stop.
    /// </summary>
    public override void OnDrop() {
        this.owner = null;
        this.countToSkip = WAITING_COUNT;
        this.latestOwnerPos = Vector3.zero;
    }

    // public override void OnPickupUseDown()
    // {
    // }

    public void FixedUpdate() {
        if (this.owner == null) {
            return;
        }

        this.EmulateFlying();
    }

    /// <summary>
    /// Emulates flying sky.
    ///
    /// Requirement:
    /// - this.owner != null
    /// </summary>
    private void EmulateFlying() {
        if (this.owner.IsPlayerGrounded()) {
            return;
        }

        if (!this.IsReadyToFixY()) {
            this.owner.SetVelocity(this.latestOwnerPos);  // Keep the position
            this.countToSkip--;
            return;
        }

        // var nextY = Mathf.Lerp(_fallStartedY, transform.position.y, 6 * Time.fixedDeltaTime);
        var nextPosition = new Vector3(0, this.latestOwnerPos.y - 0.1f, 0);
        this.owner.SetVelocity(nextPosition);

        // Wait to fly again.
        this.latestOwnerPos = this.transform.position;
        this.countToSkip = WAITING_COUNT;
    }

    private bool IsReadyToFixY() {
        if (this.countToSkip < 0) {
            Debug.Log($"Flyer: IsReadyToFixY(): Something wrong. this.countToSkip is: {this.countToSkip}");
            return false;
        }

        return (this.countToSkip != 0);
    }
}
