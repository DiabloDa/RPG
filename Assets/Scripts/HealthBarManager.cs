using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarManager : MonoBehaviour
{

    [SerializeField] private Slider HealthBar;

    [SerializeField] private float maxhealth = 100;
    [SerializeField] private float currHealth;
   
    private CharacterState characterState;
    //[SerializeField] private float CurrentHealth;

    void Start()
    {
        //characterState = HealthBar.GetComponent<CharacterState>();

       // HealthBar.value = characterState.CurrentHealth;
       
        currHealth = maxhealth;
        HealthBar.value = currHealth;
   
       
    }

    
    void Update()
    {
       // HealthBar.value = characterState.CurrentHealth;

        HealthBar.value = currHealth;

        if (Input.GetKeyDown(KeyCode.E)) 
        {

            currHealth -= 20;
        
        }

    }
}
