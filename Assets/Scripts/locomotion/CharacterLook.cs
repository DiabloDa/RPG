using System;
using Actividad2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;

namespace Clases.Clase_2.Scripts
{
    public class CharacterLook : MonoBehaviour, ICharacterComponent
    {
        [SerializeField] private Transform target;

        [SerializeField] private FloatDampener horizontalDampener;
        [SerializeField] private FloatDampener verticalDampener;

        [SerializeField] private float horizontalRotationSpeed;
        [SerializeField] private float verticalRotationSpeed;
        [SerializeField]private Vector2 verticalRotationLimits;

        [SerializeField] private float lockTurnSpeed = 360f;
        
        private float verticalRotation;
        public void OnLook(InputAction.CallbackContext ctx)
        {
            Vector2 inputValue = ctx.ReadValue<Vector2>();
            inputValue = inputValue / new Vector2(Screen.width, Screen.height);
            horizontalDampener.TargetValue = inputValue.x;
            verticalDampener.TargetValue = inputValue.y;
        }
        private void ApplyLookRotation()
        {

            if (target == null)
            {
                throw new NullReferenceException("Look target is null");
            }

            if (ParentCharacter != null && ParentCharacter.LockTarget != null)
            {
                Vector3 toTarget = ParentCharacter.LockTarget.position - target.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    target.rotation = Quaternion.RotateTowards(target.rotation, desired, lockTurnSpeed * Time.deltaTime);

                    Vector3 euler = target.localEulerAngles;
                    verticalRotation = euler.x;
                    if (verticalRotation > 180f) verticalRotation -= 360f;
                    verticalRotation = Mathf.Clamp(verticalRotation, verticalRotationLimits.x, verticalRotationLimits.y);
                    euler.x = verticalRotation;
                    target.localEulerAngles = euler;
                }

                return;
            }

            target.RotateAround(target.position, transform.up, horizontalDampener.CurrentValue * horizontalRotationSpeed * 360 * Time.deltaTime);
            verticalRotation += verticalDampener.CurrentValue * verticalRotationSpeed * 360 * Time.deltaTime;
            verticalRotation = Mathf.Clamp(verticalRotation,verticalRotationLimits.x, verticalRotationLimits.y);

            Vector3 manualEuler = target.localEulerAngles;
            manualEuler.x = verticalRotation;
            target.localEulerAngles = manualEuler;
        }
        private void Update()
        {
            horizontalDampener.Update();
            verticalDampener.Update();
            ApplyLookRotation();
        }
        [field:SerializeField] public Character ParentCharacter { get; set; }
    }
}