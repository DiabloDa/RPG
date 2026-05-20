using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class ChangeCharacter : MonoBehaviour
{

    [SerializeField] private GameObject character1;
    [SerializeField] private GameObject character2;

    [SerializeField] private GameObject char1Pos;
    [SerializeField] private GameObject char2Pos;

    [SerializeField] private GameObject HPBar1;
    [SerializeField] private GameObject HPBar2;

    [SerializeField] private GameObject camara1;
    [SerializeField] private GameObject camara2;
    void Start()
    {
        character1.SetActive(true);    
        character2.SetActive(false);

        HPBar1.SetActive(true);
        HPBar2.SetActive(false);

        camara1.SetActive(true);
        camara2.SetActive(false);
    }
    
    public void Update()
    {
        SwapCharacter();
        SwapCamera();

      
     
    }


    public void SwapCharacter()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            char1Pos.transform.position = char2Pos.transform.position;
            
            character1.SetActive(true) ;
            character2 .SetActive(false) ;

            HPBar1.SetActive(true);
            HPBar2.SetActive(false);
            
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) 
        {
            char2Pos.transform.position = char1Pos.transform.position;

            character2.SetActive(true) ; 
            character1 .SetActive(false) ;

            HPBar2.SetActive(true);
            HPBar1.SetActive(false);
        }
    }

    public void SwapCamera()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            camara1.SetActive(true) ;
            camara2.SetActive(false) ;

        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            camara2.SetActive(true) ;
            camara1.SetActive(false) ;

        }

    }


}
