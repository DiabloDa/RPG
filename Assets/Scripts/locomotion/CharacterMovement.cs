using System;
using System.Numerics;
using Actividad2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Clases.Clase_2.Scripts
{
    public class CharacterMovement : MonoBehaviour ,ICharacterComponent
    {

        [SerializeField] private FloatDampener speedX;
        [SerializeField] private FloatDampener speedY;
        [SerializeField] private Camera camera;
        [SerializeField] private float angularSpeed;

        [Header("Facing")]
        [Tooltip("If true, the character will face the camera's yaw (projected on the floor) even when not aiming.")]
        [SerializeField] private bool alwaysFaceCameraYaw = true;
        [Tooltip("If true, the character can rotate to camera yaw even while standing still.")]
        [SerializeField] private bool rotateToCameraWhenIdle = true;

        [Header("Facing Smoothing")]
        [Tooltip("Smooths rotation when aligning to camera yaw (does not affect attack-direction rotation).")]
        [SerializeField] private bool smoothFaceCameraYaw = true;
        [SerializeField, Min(0f)] private float faceCameraYawSmoothing = 10f;
        [Tooltip("For a brief moment after an attack ends, use a slower smoothing value so the character doesn't snap back abruptly.")]
        [SerializeField, Min(0f)] private float faceCameraYawSmoothingAfterAttack = 4f;
        [SerializeField, Min(0f)] private float afterAttackSmoothingSeconds = 0.25f;

        [Header("Tuning")]
        [SerializeField] private float movementInputScale = 1f;
        private Quaternion targetRotation;
        
        private int _speedXHash;
        private int _speedYHash;
        private Animator _animator;
        private AttackController _attackController;

        private bool _wasAttackLocked;
        private float _afterAttackTimer;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (camera == null)
            {
                camera = Camera.main;
            }

            _attackController = GetComponent<AttackController>();
            if (_attackController == null)
            {
                _attackController = GetComponentInParent<AttackController>();
            }
            if (_attackController == null)
            {
                _attackController = GetComponentInChildren<AttackController>(true);
            }

            _speedXHash = Animator.StringToHash("SpeedX");
            _speedYHash = Animator.StringToHash("SpeedY");
        }

        private void SolveCharacterRotation()
        {
#if UNITY_EDITOR
            // Uncomment temporarily if you need to debug rotation solve.
            // Debug.Log("[CharacterMovement] Solve rotations");
#endif
            if (camera == null)
            {
                return;
            }

            Vector3 floorNormal = transform.up;
            Vector3 cameraRealForward = camera.transform.forward;
            float angleInterpolator = Mathf.Abs(Vector3.Dot(cameraRealForward, floorNormal));
            Vector3 cameraForward = Vector3.Lerp(cameraRealForward, camera.transform.up, angleInterpolator).normalized;

            Vector3 planarForward = Vector3.ProjectOnPlane(cameraForward, floorNormal);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                planarForward = Vector3.ProjectOnPlane(cameraRealForward, floorNormal);
            }

            if (planarForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            planarForward = planarForward.normalized;
            Debug.DrawLine(transform.position, transform.position + planarForward * 3f, Color.green, 0.1f);
            targetRotation = Quaternion.LookRotation(planarForward, floorNormal);
        }

        public void OnMove(InputAction.CallbackContext ctx)
        {
            Vector2 inputValue = ctx.ReadValue<Vector2>();

            // Keep attack direction selection in "camera space" (WASD meaning doesn't change when the character is facing elsewhere).
            _attackController?.SetMoveInput(inputValue);

            // Movement blend tree expects local-space inputs. Convert camera-relative WASD into local X/Z so controls stay consistent
            // even if the character is still facing the last attack direction.
            if (camera == null)
            {
                speedX.TargetValue = inputValue.x;
                speedY.TargetValue = inputValue.y;
                return;
            }

            Vector2 clampedInput = inputValue;
            if (clampedInput.sqrMagnitude > 1f)
            {
                clampedInput.Normalize();
            }

            Vector3 floorNormal = transform.up;
            Vector3 cameraRealForward = camera.transform.forward;
            float angleInterpolator = Mathf.Abs(Vector3.Dot(cameraRealForward, floorNormal));
            Vector3 cameraForward = Vector3.Lerp(cameraRealForward, camera.transform.up, angleInterpolator).normalized;

            Vector3 planarForward = Vector3.ProjectOnPlane(cameraForward, floorNormal);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                planarForward = Vector3.ProjectOnPlane(cameraRealForward, floorNormal);
            }
            planarForward = planarForward.normalized;
            Vector3 planarRight = Vector3.Cross(floorNormal, planarForward).normalized;

            Vector3 worldMove = planarRight * clampedInput.x + planarForward * clampedInput.y;
            Vector3 localMove = transform.InverseTransformDirection(worldMove);

            speedX.TargetValue = localMove.x;
            speedY.TargetValue = localMove.z;
        }
        private void Update()
        {
            speedX.Update();
            speedY.Update();

            float characterMultiplier = ParentCharacter != null ? ParentCharacter.MoveInputMultiplier : 1f;
            float appliedScale = movementInputScale * characterMultiplier;

            // While an attack has locked a direction, don't overwrite the parameters used by the attack blend tree.
            if (_attackController == null || !_attackController.IsAttackDirectionLocked)
            {
                _animator.SetFloat(_speedXHash, speedX.CurrentValue * appliedScale);
                _animator.SetFloat(_speedYHash, speedY.CurrentValue * appliedScale);
            }

            bool isAiming = ParentCharacter != null && ParentCharacter.IsAiming;

            bool attackLocked = _attackController != null && (_attackController.IsAttackDirectionLocked || _attackController.IsAttacking);

            if (attackLocked)
            {
                _wasAttackLocked = true;
                _afterAttackTimer = 0f;
            }
            else if (_wasAttackLocked)
            {
                _wasAttackLocked = false;
                _afterAttackTimer = afterAttackSmoothingSeconds;
            }

            if (_afterAttackTimer > 0f)
            {
                _afterAttackTimer = Mathf.Max(0f, _afterAttackTimer - Time.deltaTime);
            }

            bool shouldFaceCamera = (alwaysFaceCameraYaw || isAiming) && !attackLocked;

            // Only align the character to the camera while aiming. Otherwise, preserve the current facing
            // (e.g. keep the direction you last attacked towards instead of snapping back to "front").
            if (shouldFaceCamera)
            {
                SolveCharacterRotation();
                ApplyCharacterRotation(rotateToCameraWhenIdle);
            }
        }

        private void ApplyCharacterRotation(bool allowIdleRotation)
        {
            float motionMagnitud = Mathf.Sqrt(speedX.TargetValue * speedX.TargetValue + speedY.TargetValue * speedY.TargetValue);
            float rotationSpeed = allowIdleRotation ? 1f : Mathf.SmoothStep(0, .01f, motionMagnitud);

            if (rotationSpeed <= 0f)
            {
                return;
            }

            if (smoothFaceCameraYaw)
            {
                float k = _afterAttackTimer > 0f ? faceCameraYawSmoothingAfterAttack : faceCameraYawSmoothing;
                k = Mathf.Max(0f, k);
                float t = k <= 0f ? 1f : 1f - Mathf.Exp(-k * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, angularSpeed * rotationSpeed);
        }

        public Character ParentCharacter { get; set; }
    }
}