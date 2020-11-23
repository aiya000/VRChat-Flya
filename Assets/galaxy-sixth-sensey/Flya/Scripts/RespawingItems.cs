using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class RespawingItems : UdonSharpBehaviour {
    [SerializeField]
    private Transform[] items;

    private Vector3[] initialItemsPositions;

    public void Start() {
        if (this.items == null) {
            Debug.Log("RespawingItems: Start(): this.items is not set.");
            return;
        }

        this.initialItemsPositions = new Vector3[this.items.Length];

        for (var i = 0; i < this.items.Length; i++) {
            this.initialItemsPositions[i] = this.items[i].position;
        }
    }

    public override void Interact() {
        if (this.items == null) {
            Debug.Log("RespawingItems: Interact(): this.items is not set.");
            return;
        }

        for (var i = 0; i < this.items.Length; i++) {
            this.items[i].position = this.initialItemsPositions[i];
        }
    }
}
