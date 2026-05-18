using UnityEngine;

public class ChangeCharacter : MonoBehaviour
{

    [SerializeField] private GameObject character1;
    [SerializeField] private GameObject character2;
    void Start()
    {
        character1.SetActive(true);    
        character2.SetActive(false);
    }


    public void SwapCharacter()
    {



    }


   
}
