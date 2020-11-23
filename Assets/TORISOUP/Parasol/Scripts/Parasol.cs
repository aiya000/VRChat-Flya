/*
 * Copyright (c) 2020 TORISOUP
 * Released under the MIT license
 * https://opensource.org/licenses/mit-license.php
*/

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace TORISOUP
{
    public class Parasol : UdonSharpBehaviour
    {
        /// <summary>
        /// ローカルでのみ動作するオブジェクトか
        /// Global化する場合はfalseにする
        /// </summary>
        [SerializeField] private bool _isGlobal;
    
        /// <summary>
        /// 傘を開いている時に描画するオブジェクト
        /// </summary>
        [SerializeField] private GameObject _openParasol;

        /// <summary>
        /// 傘を閉じている時に描画するオブジェクト
        /// </summary>
        [SerializeField] private GameObject _closedParasol;

        /// <summary>
        /// 音を再生する時に使うAudioSource
        /// </summary>
        [SerializeField] private AudioSource _audioSource;

        /// <summary>
        /// 傘を開いた時に鳴る音
        /// </summary>
        [SerializeField] private AudioClip _open;

        /// <summary>
        /// 傘を閉じた時に鳴る音
        /// </summary>
        [SerializeField] private AudioClip _close;

        /// <summary>
        /// 風の影響を受けるか
        /// </summary>
        [SerializeField] private bool _enableWindEffect;

        /// <summary>
        /// 風の判定を行うレイヤ
        /// </summary>
        [SerializeField] private LayerMask _windLayer;

        /// <summary>
        /// パラソルが開いているか
        /// </summary>mo
        private bool _isParasolOpened;

        /// <summary>
        /// 誰かが持っているか
        /// </summary>
        private bool _isGrab;

        /// <summary>
        /// 風の中にいるか
        /// </summary>
        private bool _isInWind;

        /// <summary>
        /// 風の源のTransform
        /// </summary>
        private Transform _windTransform;

        private VRCPlayerApi _player;

        /// <summary>
        /// 接地しているか
        /// </summary>
        private bool _isGrounded;

        /// <summary>
        /// 落下開始位置
        /// </summary>
        private float _fallStartedY;

        /// <summary>
        /// 横風の影響力
        /// </summary>
        private float _crossWindPower = 7.0f;

        private void Start()
        {
            _isParasolOpened = false;
            _openParasol.SetActive(false);
            _closedParasol.SetActive(true);
        }

   
        public override void OnPickup()
        {
            _player = Networking.LocalPlayer;
            _isGrab = true;
        }

        public override void OnDrop()
        {
            _isGrab = false;
        }

        public override void OnPickupUseDown()
        {
            if (_isParasolOpened)
            {
                if (_isGlobal)
                {
                    // グローバル動作時は傘の開閉を同期する
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(CloseParasol));
                }
                else
                {
                    CloseParasol();
                }
            }
            else
            {
                if (_isGlobal)
                {
                    // グローバル動作時は傘の開閉を同期する
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OpenParasol));
                }
                else
                {
                    OpenParasol();
                }

                // 傘を開いた位置を落下開始位置とする
                UpdateFallsStartPoint();
            }
        }

        /// <summary>
        /// 現在の高さを落下開始位置とする
        /// </summary>
        private void UpdateFallsStartPoint()
        {
            if (_player == null) return;
            _fallStartedY = transform.position.y;
        }

        void FixedUpdate()
        {
            if (!_isGrab) return;
            if (!_isParasolOpened) return;
            if (_player == null) return;

            Core();
        }

        private void Core()
        {
            if (_isGrounded && !_player.IsPlayerGrounded())
            {
                // 落下開始したなら落下位置更新
                UpdateFallsStartPoint();
            }

            _isGrounded = _player.IsPlayerGrounded();
            if (_isGrounded) return;

            // ひっくり返してるなら何もしない
            if (Vector3.Dot(transform.up, Vector3.down) > 0f)
            {
                return;
            }

            // 落下分
            var deltaY = _fallStartedY - transform.position.y;

            if (deltaY < 0)
            {
                // 落下開始位置より上に来た場合は更新
                UpdateFallsStartPoint();
                return;
            }

            // 横風成分
            var crossWind = Vector3.zero;

            if (_isInWind)
            {
                // 風の向き（Up）
                var windDirection = Vector3.zero;
                if (_windTransform != null)
                {
                    windDirection = _windTransform.up;
                }
                
                // 風の縦方向成分は落下位置の上書きに使う
                _fallStartedY = transform.position.y + 3.0f * windDirection.y;
                // 風の横方向成分は移動速度に反映
                crossWind = _crossWindPower * new Vector3(windDirection.x, 0, windDirection.z);
            }
            else
            {
                // 徐々に落下させる
                _fallStartedY = Mathf.Lerp(_fallStartedY, transform.position.y, 5 * Time.fixedDeltaTime);
            }

            // 上書きする速度を計算
            var nextVelocity = transform.up.normalized
                               * Mathf.Sqrt(2 * Physics.gravity.magnitude * deltaY)
                               + crossWind;

            var currentVel = _player.GetVelocity();
            // Lerpを挟むことでプレイヤの操作と合成する
            // 補間値を小さくすると落下が早くなる
            var targetVel = Vector3.Lerp(currentVel, nextVelocity, 4 * Time.fixedDeltaTime);

            _player.SetVelocity(targetVel);
        }

        public void OpenParasol()
        {
            _isParasolOpened = true;
            _openParasol.SetActive(true);
            _closedParasol.SetActive(false);
            if (_open != null) _audioSource.PlayOneShot(_open);
        }

        public void CloseParasol()
        {
            _isParasolOpened = false;
            _openParasol.SetActive(false);
            _closedParasol.SetActive(true);
            if (_close != null) _audioSource.PlayOneShot(_close);
        }

        /// <summary>
        /// 風に触れたか
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!_enableWindEffect) return;
            if (other == null) return;
            if (((1 << other.gameObject.layer) & _windLayer.value) != 0)
            {
                _isInWind = true;
                _windTransform = other.transform;
            }
        }

        /// <summary>
        /// 風から出たか
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            if (!_enableWindEffect) return;
            if (other == null) return;
            if (((1 << other.gameObject.layer) & _windLayer.value) != 0)
            {
                _isInWind = false;
            }
        }
    }
}