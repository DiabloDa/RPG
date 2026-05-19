using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    [SerializeField] private float _startStamina = 1000;
    [SerializeField] private float _staminaRegen = 75f;
    [SerializeField] private float _currentStamina = 100;

    [SerializeField] private float _startHealth = 100;
    [SerializeField] private float _currentHealth = 100;

    private bool _hasDied;

    public float CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0f;

    public event Action Died;

    public float CurrentStamina => _currentStamina;

    private void Start()
    {
        _currentStamina = _startStamina;
        _currentHealth = _startHealth;
        _hasDied = _currentHealth <= 0f;
    }

    public void ResetState()
    {
        _currentStamina = _startStamina;
        _currentHealth = _startHealth;
        _hasDied = false;
    }

    private void Update()
    {
        RegenerateStamina(_staminaRegen * Time.deltaTime);
    }

    private void RegenerateStamina(float staminaRegen)
    {

        _currentStamina = Mathf.Min(CurrentStamina+staminaRegen, _startStamina);

    }
    
    private float GetStaminaDepletion()
    {
        return 10;
    }

    public bool HasStaminaForCost(float staminaCost)
    {
        if (staminaCost <= 0)
        {
            return true;
        }

        return CurrentStamina >= GetStaminaDepletion() * staminaCost;
    }

    public bool TryDepleteStamina(float staminaCost)
    {
        if (!HasStaminaForCost(staminaCost))
        {
            return false;
        }

        DepleteStamina(staminaCost);
        return true;
    }

    public void DepleteStamina(float staminaDepletion)
    { 
        _currentStamina = Mathf.Max(0f, CurrentStamina - GetStaminaDepletion() * staminaDepletion);
    
    
    }


    public void DepleteHealth(float healthDepletion, out bool zeroHealth)
    {
        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - healthDepletion);
        zeroHealth = false;

        DevDebug.LogPlayerHealth($"{gameObject.name}: {previousHealth} -> {_currentHealth} (-{healthDepletion})");

        if (_currentHealth <= 0f)
        {
            zeroHealth = true;

            if (!_hasDied)
            {
                _hasDied = true;
                Died?.Invoke();
            }
        }


    }






}
