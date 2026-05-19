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


    private CharacterState playerOneCharacter;
    private CharacterStateCombatAdapter playerOneAdapter;
    private PlayerCombatFacade playerOneFacade;
    private PlayerPowerUpController playerPowerUps;

    public IPlayerCombat PlayerOne => playerOneFacade;
    public PlayerPowerUpController PlayerPowerUps => playerPowerUps;


    private void Awake()
    {
        CreatePlayer();
        CreatePowerUpSystem();
    }

    private void CreatePlayer()
    {
        if (playerOneFacade != null)
        {
            return;
        }

        GameObject playerGo = new GameObject("[Player 1]");
        playerOneCharacter = playerGo.AddComponent<CharacterState>();
        playerOneAdapter = new CharacterStateCombatAdapter(playerOneCharacter);
        playerOneFacade = new PlayerCombatFacade(playerOneAdapter);
        DontDestroyOnLoad(playerGo);
    }

    private void CreatePowerUpSystem()
    {
        if (playerPowerUps != null)
        {
            return;
        }

        playerPowerUps = GetComponent<PlayerPowerUpController>();
        if (playerPowerUps == null)
        {
            playerPowerUps = gameObject.AddComponent<PlayerPowerUpController>();
        }

        playerPowerUps.Initialize(playerOneFacade, playerOneAdapter);

    }

    public void ResetRun()
    {
        if (playerOneCharacter != null)
        {
            playerOneCharacter.ResetState();
        }

        if (playerPowerUps != null)
        {
            playerPowerUps.ResetPowerUps();
        }
    }

}
