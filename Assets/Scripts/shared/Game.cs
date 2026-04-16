using UnityEngine;

public class Game : MonoBehaviour
{
    private static Game _instance;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateGame()
    {
        GameObject GameOb = new GameObject("Game");
        _instance = GameOb.AddComponent<Game>();
        DontDestroyOnLoad(GameOb);
    
    }

    public static Game Instance
    {
        get
        {
             if ( _instance == null )
             {
                CreateGame();
             }
        
        return _instance;
        }



    }


    private CharacterState playerOne;
    public CharacterState PlayerOne => playerOne;


    private void Awake()
    {
        CreatePlayer();
    }

    private void CreatePlayer()
    {
        GameObject playerGo = new GameObject("[Player 1]");
        playerOne = playerGo.AddComponent<CharacterState>();    
        DontDestroyOnLoad(playerGo);

    }

}
