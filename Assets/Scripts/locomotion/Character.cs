using System;
using UnityEngine;


    [DefaultExecutionOrder(-1)]
    public class Character : MonoBehaviour
    {
        private bool isAiming;
        private bool isFiring;
        private bool isReloading;
        private bool isStealth;
        private bool isWeaponHolstered;
        private bool isWeaponTransitioning;
        [SerializeField] private float defaultMoveInputMultiplier = 1f;

        private float moveInputMultiplier = 1f;
        private Transform lockTarget;

        public bool IsAiming
        {
            get => isAiming;
            set => isAiming = value;
        }

        public bool IsFiring
        {
            get => isFiring;
            set => isFiring = value;
        }

        public bool IsReloading
        {
            get => isReloading;
            set => isReloading = value;
        }

        public bool IsStealth
        {
            get => isStealth;
            set => isStealth = value;
        }

        /// <summary>
        /// True when the weapon is stored/hidden. While true, the player cannot shoot, aim or reload.
        /// </summary>
        public bool IsWeaponHolstered
        {
            get => isWeaponHolstered;
            set => isWeaponHolstered = value;
        }

        /// <summary>
        /// True while playing equip/holster transition animation.
        /// Treated the same as holstered for gameplay blocking.
        /// </summary>
        public bool IsWeaponTransitioning
        {
            get => isWeaponTransitioning;
            set => isWeaponTransitioning = value;
        }

        /// <summary>
        /// Multiplier applied to movement input/animation parameters.
        /// Default is 1. Sigilo can set this lower.
        /// </summary>
        public float MoveInputMultiplier
        {
            get => moveInputMultiplier;
            set => moveInputMultiplier = Mathf.Clamp(value, 0f, 1f);
        }
        

        public Transform LockTarget
        {
            get => lockTarget;
            set => lockTarget = value;

        }

        private void Awake()
        {
            MoveInputMultiplier = defaultMoveInputMultiplier;
            RegisterComponents();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void RegisterComponents()
        {
            foreach (ICharacterComponent component in GetComponentsInChildren<ICharacterComponent>())
            {
                component.ParentCharacter = this;
            }
        }
    }
