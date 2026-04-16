using UnityEngine;

public class CharacterState : MonoBehaviour
{
    [SerializeField] private float _startStamina = 1000;
    [SerializeField] private float _staminaRegen = 75f;
    [SerializeField] private float _currentStamina = 100;

    [SerializeField] private float _startHealth = 100;
    [SerializeField] private float _currentHealth = 100;

    public float CurrentStamina => _currentStamina;

    private void Start()
    {
        _currentStamina = _startStamina;
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
        _currentHealth -= healthDepletion;
        zeroHealth = false;

        if (_currentHealth <= 0)
        {
            zeroHealth = true;
        }


    }






}
