using System;
using UnityEngine;
using Unity.Cinemachine;

public class CameraImpactShake : MonoBehaviour
{
    [Serializable]
    private struct ShakePreset
    {
        [Range(0f, 1f)] public float trauma;
        [Min(0f)] public float duration;
        [Min(0f)] public float frequency;
        [Min(0f)] public float positionAmplitude;
        [Min(0f)] public float rotationAmplitudeDegrees;
    }

    private static CameraImpactShake _instance;
    public static CameraImpactShake Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            // Don't auto-add components. Auto-adding can make it look like disabling the script
            // "does nothing" because another instance gets created.
            var main = Camera.main;
            if (main != null && main.TryGetComponent(out CameraImpactShake camShake))
            {
                _instance = camShake;
                return _instance;
            }

            // Find an existing instance in the loaded scenes (includes disabled objects).
            var all = Resources.FindObjectsOfTypeAll<CameraImpactShake>();
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s != null && s.gameObject.scene.IsValid())
                {
                    _instance = s;
                    return _instance;
                }
            }

            return null;
        }
    }

    [Header("Presets (vary by attack type)")]
    [SerializeField] private ShakePreset small = new ShakePreset
    {
        trauma = 0.20f,
        duration = 0.10f,
        frequency = 22f,
        positionAmplitude = 0.03f,
        rotationAmplitudeDegrees = 0.6f,
    };

    [SerializeField] private ShakePreset medium = new ShakePreset
    {
        trauma = 0.35f,
        duration = 0.12f,
        frequency = 26f,
        positionAmplitude = 0.06f,
        rotationAmplitudeDegrees = 1.2f,
    };

    [SerializeField] private ShakePreset big = new ShakePreset
    {
        trauma = 0.60f,
        duration = 0.16f,
        frequency = 30f,
        positionAmplitude = 0.10f,
        rotationAmplitudeDegrees = 2.4f,
    };

    [Header("Safety / readability")]
    [SerializeField] private bool affectPosition = true;
    [SerializeField] private bool affectRotation = true;
    [SerializeField, Range(0f, 3f)] private float globalIntensity = 1f;

    [Header("Cinemachine")]
    [SerializeField] private bool preferCinemachineImpulse = true;
    [SerializeField, Min(0f)] private float cinemachineListenerGain = 1.0f;
    [SerializeField, Min(0f)] private float cinemachineForceMultiplier = 1.0f;

    [Header("Cinemachine (secondary reaction)")]
    [SerializeField] private bool cinemachineUseReactionNoise = true;
    [SerializeField, Min(0f)] private float cinemachineReactionAmplitudeGain = 1.0f;
    [SerializeField, Min(0f)] private float cinemachineReactionFrequencyGain = 1.0f;
    [SerializeField, Min(0f)] private float cinemachineReactionDuration = 0.20f;
    [SerializeField, Min(0f)] private float cinemachineReactionPositionAmplitude = 0.02f;
    [SerializeField, Min(0f)] private float cinemachineReactionRotationAmplitudeDegrees = 1.2f;

    [Header("Light charge (camera up/down)")]
    [SerializeField] private bool lightChargeEnabled = true;
    [SerializeField, Min(0f)] private float lightChargeRaiseY = 0.12f;
    [SerializeField, Min(0.1f)] private float lightChargeUpSpeed = 6.0f;
    [SerializeField, Min(0.1f)] private float lightChargeDownSpeed = 10.0f;

    [Header("Heavy charge (camera up/down)")]
    [SerializeField] private bool heavyChargeEnabled = true;
    [SerializeField, Min(0f)] private float heavyChargeRaiseY = 0.35f;
    [SerializeField, Min(0.1f)] private float heavyChargeUpSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float heavyChargeDownSpeed = 8.0f;

    private CinemachineExternalImpulseListener _extListener;
    private bool _extListenerCreatedByUs;
    private bool _extListenerSettingsCached;
    private int _cachedChannelMask;
    private float _cachedGain;
    private bool _cachedUse2DDistance;
    private bool _cachedUseLocalSpace;
    private CinemachineImpulseListener.ImpulseReaction _cachedReactionSettings;

    private CinemachineThirdPersonFollow _cmThirdPersonFollow;
    private CinemachineFollow _cmFollow;
    private bool _cmBaseOffsetCached;
    private Vector3 _cmBaseShoulderOffset;
    private Vector3 _cmBaseFollowOffset;
    private float _charge01;
    private float _chargeTarget01;
    private float _chargeRaiseY;
    private float _chargeUpSpeed;
    private float _chargeDownSpeed;

    private float _timeRemaining;
    private float _frequency;
    private float _positionAmp;
    private float _rotationAmpDeg;

    private Vector3 _lastPosOffset;
    private Quaternion _lastRotOffset = Quaternion.identity;

    private float _seedX;
    private float _seedY;
    private float _seedRot;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }

        _seedX = UnityEngine.Random.value * 1000f;
        _seedY = UnityEngine.Random.value * 1000f;
        _seedRot = UnityEngine.Random.value * 1000f;
    }

    private void OnDisable()
    {
        // Remove any residual offsets to avoid leaving the camera displaced.
        RemoveLastOffsets();

        // Restore any charge offset we applied to the active virtual camera.
        if (_cmBaseOffsetCached)
        {
            if (_cmFollow != null)
            {
                _cmFollow.FollowOffset = _cmBaseFollowOffset;
            }

            if (_cmThirdPersonFollow != null)
            {
                _cmThirdPersonFollow.ShoulderOffset = _cmBaseShoulderOffset;
            }
        }

        // If we auto-added/configured a Cinemachine listener, mute/restore it so disabling this
        // script clearly disables shake (and doesn't leave modified settings behind).
        if (_extListener != null)
        {
            if (_extListenerSettingsCached)
            {
                _extListener.ChannelMask = _cachedChannelMask;
                _extListener.Gain = _cachedGain;
                _extListener.Use2DDistance = _cachedUse2DDistance;
                _extListener.UseLocalSpace = _cachedUseLocalSpace;
                _extListener.ReactionSettings = _cachedReactionSettings;
            }
            else if (_extListenerCreatedByUs)
            {
                // Don't disable the component (could leave the camera offset). Mute it instead.
                _extListener.ChannelMask = 0;
                _extListener.Gain = 0f;
                var reaction = _extListener.ReactionSettings;
                reaction.m_SecondaryNoise = null;
                _extListener.ReactionSettings = reaction;
            }
        }
    }

    private void Update()
    {
        UpdateChargeOffset();
    }

    private void LateUpdate()
    {
        if (_timeRemaining <= 0f)
        {
            // Ensure we don't leave any residual offsets.
            RemoveLastOffsets();
            return;
        }

        // First, remove last frame offsets so we always apply shake relative to the camera's base pose.
        RemoveLastOffsets();

        _timeRemaining -= Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        float phase = t * _frequency;

        float nx = Mathf.PerlinNoise(_seedX, phase) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_seedY, phase) * 2f - 1f;
        float nr = Mathf.PerlinNoise(_seedRot, phase) * 2f - 1f;

        // Ease out to keep readability (no sudden snaps at the end).
        float fade = Mathf.Clamp01(_timeRemaining / Mathf.Max(0.0001f, _totalDuration));
        float intensity = globalIntensity * fade;

        if (affectPosition)
        {
            _lastPosOffset = new Vector3(nx, ny, 0f) * (_positionAmp * intensity);
            transform.localPosition += _lastPosOffset;
        }

        if (affectRotation)
        {
            float angle = nr * (_rotationAmpDeg * intensity);
            _lastRotOffset = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.localRotation = transform.localRotation * _lastRotOffset;
        }
    }

    private float _totalDuration;

    private void RemoveLastOffsets()
    {
        if (_lastPosOffset != Vector3.zero)
        {
            transform.localPosition -= _lastPosOffset;
            _lastPosOffset = Vector3.zero;
        }

        if (_lastRotOffset != Quaternion.identity)
        {
            transform.localRotation = transform.localRotation * Quaternion.Inverse(_lastRotOffset);
            _lastRotOffset = Quaternion.identity;
        }
    }

    public void Impact(DamageMessage.DamageLevel damageLevel)
    {
        Impact(damageLevel, -1f);
    }

    public void Impact(DamageMessage.DamageLevel damageLevel, float durationOverrideSeconds)
    {
        // If this component is disabled, don't generate impulses or legacy shake.
        if (!isActiveAndEnabled)
        {
            return;
        }

        // Preset drives both Cinemachine and fallback so tuning works.
        ShakePreset preset = damageLevel switch
        {
            DamageMessage.DamageLevel.Small => small,
            DamageMessage.DamageLevel.Medium => medium,
            _ => big,
        };

        float effectiveDuration = durationOverrideSeconds > 0f ? durationOverrideSeconds : preset.duration;
        bool extendReactionToMatch = durationOverrideSeconds > 0f;

        // If you're using CinemachineBrain + CinemachineCamera, this component should use Impulses.
        if (preferCinemachineImpulse && TryCinemachineImpulse(
                preset,
                globalIntensity,
                cinemachineListenerGain,
                cinemachineForceMultiplier,
                cinemachineUseReactionNoise,
                cinemachineReactionAmplitudeGain,
                cinemachineReactionFrequencyGain,
                cinemachineReactionDuration,
                cinemachineReactionPositionAmplitude,
                cinemachineReactionRotationAmplitudeDegrees,
                effectiveDuration,
                extendReactionToMatch))
        {
            // Ensure we don't stack the legacy transform shake on top of Cinemachine (can feel "jumpy").
            _timeRemaining = 0f;
            _totalDuration = 0f;
            RemoveLastOffsets();
            return;
        }

        // Fallback (non-Cinemachine): simple transform shake.

        // If no shake is running, start fresh with this preset.
        if (_timeRemaining <= 0f)
        {
            _timeRemaining = effectiveDuration;
            _totalDuration = effectiveDuration;
            _frequency = preset.frequency;
            _positionAmp = preset.positionAmplitude;
            _rotationAmpDeg = preset.rotationAmplitudeDegrees;
            return;
        }

        // Otherwise stack: extend time and take the max amplitudes/frequency.
        _timeRemaining = Mathf.Max(_timeRemaining, effectiveDuration);
        _totalDuration = Mathf.Max(_totalDuration, _timeRemaining);

        _frequency = Mathf.Max(_frequency, preset.frequency);
        _positionAmp = Mathf.Max(_positionAmp, preset.positionAmplitude);
        _rotationAmpDeg = Mathf.Max(_rotationAmpDeg, preset.rotationAmplitudeDegrees);
    }

    private static NoiseSettings s_reactionNoise;

    private bool TryCinemachineImpulse(
        ShakePreset preset,
        float intensity,
        float listenerGain,
        float forceMultiplier,
        bool useReactionNoise,
        float reactionAmplitudeGain,
        float reactionFrequencyGain,
        float reactionDuration,
        float reactionPositionAmp,
        float reactionRotationAmpDeg,
        float impulseDuration,
        bool extendReactionToImpulseDuration)
    {
        // Prefer shaking the camera that this component is attached to.
        var outputCam = GetComponent<Camera>();
        if (outputCam == null)
        {
            outputCam = Camera.main;
        }

        if (outputCam == null)
        {
            return false;
        }

        // Ensure an External Impulse Listener exists on the output camera.
        if (_extListener == null || _extListener.gameObject != outputCam.gameObject)
        {
            if (!outputCam.TryGetComponent(out _extListener))
            {
                _extListener = outputCam.gameObject.AddComponent<CinemachineExternalImpulseListener>();
                _extListenerCreatedByUs = true;
                _extListenerSettingsCached = false;
            }
            else
            {
                _extListenerCreatedByUs = false;
                _extListenerSettingsCached = false;
            }
        }

        if (!_extListenerCreatedByUs && !_extListenerSettingsCached)
        {
            _cachedChannelMask = _extListener.ChannelMask;
            _cachedGain = _extListener.Gain;
            _cachedUse2DDistance = _extListener.Use2DDistance;
            _cachedUseLocalSpace = _extListener.UseLocalSpace;
            _cachedReactionSettings = _extListener.ReactionSettings;
            _extListenerSettingsCached = true;
        }

        _extListener.enabled = true;


        // Keep this conservative; large Gain quickly becomes unreadable.
        _extListener.ChannelMask = 1;
        _extListener.Gain = Mathf.Max(0f, listenerGain);
        _extListener.Use2DDistance = false;
        _extListener.UseLocalSpace = true;

        // Optional secondary reaction (adds rotation + vibration; makes hits feel stronger)
        if (useReactionNoise)
        {
            if (s_reactionNoise == null)
            {
                s_reactionNoise = ScriptableObject.CreateInstance<NoiseSettings>();
                s_reactionNoise.hideFlags = HideFlags.HideAndDontSave;
            }

            float reactionPos = Mathf.Max(0f, preset.positionAmplitude) + Mathf.Max(0f, reactionPositionAmp);
            float reactionRot = Mathf.Max(0f, preset.rotationAmplitudeDegrees) + Mathf.Max(0f, reactionRotationAmpDeg);

            ApplyReactionNoiseProfile(s_reactionNoise, reactionPos, reactionRot);

            var reaction = _extListener.ReactionSettings;
            reaction.m_SecondaryNoise = s_reactionNoise;
            reaction.AmplitudeGain = Mathf.Max(0f, reactionAmplitudeGain);
            // Use preset frequency as a driver (bigger attacks usually feel "tighter/faster")
            float presetFreqMul = Mathf.Max(0.25f, preset.frequency / 22f);
            reaction.FrequencyGain = Mathf.Max(0f, reactionFrequencyGain) * presetFreqMul;
            float baseReactionDuration = Mathf.Max(0f, reactionDuration);
            reaction.Duration = extendReactionToImpulseDuration ? Mathf.Max(baseReactionDuration, Mathf.Max(0.01f, impulseDuration)) : baseReactionDuration;
            _extListener.ReactionSettings = reaction;
        }
        else
        {
            var reaction = _extListener.ReactionSettings;
            reaction.m_SecondaryNoise = null;
            _extListener.ReactionSettings = reaction;
        }

        // Build a small, readable impulse definition (Uniform so it doesn't depend on distance).
        var def = new CinemachineImpulseDefinition
        {
            ImpulseChannel = 1,
            ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
            ImpulseDuration = Mathf.Max(0.01f, impulseDuration),
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
            DissipationDistance = 100f,
            DissipationRate = 0.25f,
            PropagationSpeed = 343f,
        };

        // IMPORTANT: Cinemachine impulse magnitude is in world units (meters).
        float force = Mathf.Max(0f, preset.positionAmplitude);

        // Scale by global intensity (0..3) and a Cinemachine-specific multiplier.
        force *= Mathf.Clamp(intensity, 0f, 3f) * Mathf.Max(0f, forceMultiplier);

        // Direction: stable screen-space nudge with slight left/right variation (clean, not random).
        float side = (Time.frameCount & 1) == 0 ? 0.35f : -0.35f;
        Vector3 dir = (outputCam.transform.up + outputCam.transform.right * side).normalized;

        // Create the impulse at the camera location; Uniform mode makes distance irrelevant.
        var origin = outputCam.transform.position;
        def.CreateEvent(origin, dir * force);
        return true;
    }

    private static void ApplyReactionNoiseProfile(NoiseSettings noise, float positionAmp, float rotationAmpDeg)
    {
        // Two layers (low + high) feels less "robotic" than a single frequency.
        // Units: position in meters, rotation in degrees.
        float posLow = Mathf.Max(0f, positionAmp);
        float posHigh = posLow * 0.6f;

        float rotLow = Mathf.Max(0f, rotationAmpDeg);
        float rotHigh = rotLow * 0.6f;

        noise.PositionNoise = new NoiseSettings.TransformNoiseParams[2]
        {
            new NoiseSettings.TransformNoiseParams
            {
                X = new NoiseSettings.NoiseParams { Frequency = 6f,  Amplitude = posLow,  Constant = false },
                Y = new NoiseSettings.NoiseParams { Frequency = 7f,  Amplitude = posLow,  Constant = false },
                Z = new NoiseSettings.NoiseParams { Frequency = 0f,  Amplitude = 0f,     Constant = false },
            },
            new NoiseSettings.TransformNoiseParams
            {
                X = new NoiseSettings.NoiseParams { Frequency = 18f, Amplitude = posHigh, Constant = false },
                Y = new NoiseSettings.NoiseParams { Frequency = 20f, Amplitude = posHigh, Constant = false },
                Z = new NoiseSettings.NoiseParams { Frequency = 0f,  Amplitude = 0f,     Constant = false },
            },
        };

        noise.OrientationNoise = new NoiseSettings.TransformNoiseParams[2]
        {
            new NoiseSettings.TransformNoiseParams
            {
                X = new NoiseSettings.NoiseParams { Frequency = 8f,  Amplitude = rotLow * 0.5f, Constant = false },
                Y = new NoiseSettings.NoiseParams { Frequency = 0f,  Amplitude = 0f,          Constant = false },
                Z = new NoiseSettings.NoiseParams { Frequency = 9f,  Amplitude = rotLow,       Constant = false },
            },
            new NoiseSettings.TransformNoiseParams
            {
                X = new NoiseSettings.NoiseParams { Frequency = 22f, Amplitude = rotHigh * 0.5f, Constant = false },
                Y = new NoiseSettings.NoiseParams { Frequency = 0f,  Amplitude = 0f,           Constant = false },
                Z = new NoiseSettings.NoiseParams { Frequency = 24f, Amplitude = rotHigh,        Constant = false },
            },
        };
    }

    private void UpdateChargeOffset()
    {
        if (_chargeRaiseY <= 0f && _chargeTarget01 <= 0f && _charge01 <= 0f)
        {
            return;
        }

        if (_cmFollow == null && _cmThirdPersonFollow == null)
        {
            _cmBaseOffsetCached = false;
            TryResolveCinemachineFollowBody();
            if (_cmFollow == null && _cmThirdPersonFollow == null)
            {
                return;
            }
        }

        if (!_cmBaseOffsetCached)
        {
            if (_cmFollow != null)
            {
                _cmBaseFollowOffset = _cmFollow.FollowOffset;
            }

            if (_cmThirdPersonFollow != null)
            {
                _cmBaseShoulderOffset = _cmThirdPersonFollow.ShoulderOffset;
            }

            _cmBaseOffsetCached = true;
        }

        float up = Mathf.Max(0.1f, _chargeUpSpeed);
        float down = Mathf.Max(0.1f, _chargeDownSpeed);
        float speed = _chargeTarget01 > _charge01 ? up : down;
        _charge01 = Mathf.MoveTowards(_charge01, _chargeTarget01, speed * Time.unscaledDeltaTime);

        float yAdd = _chargeRaiseY * _charge01;

        if (_cmFollow != null)
        {
            var offset = _cmBaseFollowOffset;
            offset.y += yAdd;
            _cmFollow.FollowOffset = offset;
        }
        else if (_cmThirdPersonFollow != null)
        {
            var shoulder = _cmBaseShoulderOffset;
            shoulder.y += yAdd;
            _cmThirdPersonFollow.ShoulderOffset = shoulder;
        }
    }

    private void TryResolveCinemachineFollowBody()
    {
        var outputCam = GetComponent<Camera>();
        if (outputCam == null)
        {
            outputCam = Camera.main;
        }

        if (outputCam == null)
        {
            return;
        }

        var brain = outputCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = GetComponent<CinemachineBrain>();
        }

        if (brain == null)
        {
            return;
        }

        var active = brain.ActiveVirtualCamera;
        var vcamComponent = active as Component;
        var vcamGO = vcamComponent != null ? vcamComponent.gameObject : null;
        if (vcamGO == null)
        {
            return;
        }

        // Cinemachine v3 uses component-based pipelines. Prefer CinemachineFollow when present.
        _cmFollow = vcamGO.GetComponent<CinemachineFollow>();
        _cmThirdPersonFollow = _cmFollow == null ? vcamGO.GetComponent<CinemachineThirdPersonFollow>() : null;
    }

    private void BeginCharge(float raiseY, float upSpeed, float downSpeed)
    {
        _chargeRaiseY = Mathf.Max(0f, raiseY);
        _chargeUpSpeed = Mathf.Max(0.1f, upSpeed);
        _chargeDownSpeed = Mathf.Max(0.1f, downSpeed);
        _chargeTarget01 = 1f;
    }

    private void BeginHeavyCharge()
    {
        if (!heavyChargeEnabled)
        {
            return;
        }

        BeginCharge(heavyChargeRaiseY, heavyChargeUpSpeed, heavyChargeDownSpeed);
    }

    private void BeginLightCharge()
    {
        if (!lightChargeEnabled)
        {
            return;
        }

        BeginCharge(lightChargeRaiseY, lightChargeUpSpeed, lightChargeDownSpeed);
    }

    private void EndCharge()
    {
        _chargeTarget01 = 0f;
    }

    private void SnapDownCharge()
    {
        _charge01 = 0f;
        _chargeTarget01 = 0f;

        if (!_cmBaseOffsetCached)
        {
            return;
        }

        if (_cmFollow != null)
        {
            _cmFollow.FollowOffset = _cmBaseFollowOffset;
        }

        if (_cmThirdPersonFollow != null)
        {
            _cmThirdPersonFollow.ShoulderOffset = _cmBaseShoulderOffset;
        }
    }

    public static void TryImpact(DamageMessage.DamageLevel damageLevel)
    {
        var inst = Instance;
        if (inst == null || !inst.isActiveAndEnabled)
        {
            return;
        }

        inst.Impact(damageLevel);
    }

    public static void TryImpact(DamageMessage.DamageLevel damageLevel, float durationSeconds)
    {
        var inst = Instance;
        if (inst == null || !inst.isActiveAndEnabled)
        {
            return;
        }

        inst.Impact(damageLevel, durationSeconds);
    }

    public static void TryBeginCharge()
    {
        // Back-compat name: this is the HEAVY charge.
        var inst = Instance;
        if (inst == null || !inst.isActiveAndEnabled)
        {
            return;
        }

        inst.BeginHeavyCharge();
    }

    public static void TryBeginLightCharge()
    {
        var inst = Instance;
        if (inst == null || !inst.isActiveAndEnabled)
        {
            return;
        }

        inst.BeginLightCharge();
    }

    public static void TryEndCharge()
    {
        var inst = Instance;
        if (inst == null || !inst.isActiveAndEnabled)
        {
            return;
        }

        inst.EndCharge();
    }

    public static void TrySnapDownCharge()
    {
        var inst = Instance;
        if (inst == null || !inst.isActiveAndEnabled)
        {
            return;
        }

        inst.SnapDownCharge();
    }
}
