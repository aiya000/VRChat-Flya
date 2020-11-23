/*
 * Copyright (c) 2020 TORISOUP
 * Released under the MIT license
 * https://opensource.org/licenses/mit-license.php
*/

using UdonSharp;
using UnityEngine;

namespace TORISOUP
{
    public class PositionReset : UdonSharpBehaviour
    {
        /// <summary>
        /// Dropしてから元の場所に戻るまでの時間
        /// </summary>
        [SerializeField] private float _backPositionSeconds = 5;
 
        /// <summary>
        /// リスポーンする場所
        /// </summary>
        [SerializeField] private Transform _respawnTarget;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _leftTime;
        private bool _isPickedUp;
        
        private void Start()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _leftTime = 0;
        }

        private void Update()
        {
            if (!_isPickedUp)
            {
                _leftTime -= Time.deltaTime;
                if (_leftTime < 0)
                {
                    if (_respawnTarget == null)
                    {
                        transform.SetPositionAndRotation(_startPosition, _startRotation);
                    }
                    else
                    {
                        transform.SetPositionAndRotation(_respawnTarget.position, _respawnTarget.rotation);
                    }
                }
            }
        }

        public override void OnDrop()
        {
            _isPickedUp = false;
            _leftTime = _backPositionSeconds;
        }

        public override void OnPickup()
        {
            _isPickedUp = true;
        }
    }
}