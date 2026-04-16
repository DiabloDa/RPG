using System;
using UnityEngine;
using UnityEngine.PlayerLoop;


namespace Actividad2
{
    [Serializable]
    public struct FloatDampener
    {
        [SerializeField] private float _smoothTime;

        private float CurrentVelocity;
        public float TargetValue { get; set; }

        public float CurrentValue { get; private set; }


        public void Update()
        {
                CurrentValue = Mathf.SmoothDamp(CurrentValue, TargetValue, ref CurrentVelocity, _smoothTime);
        }




    }

}