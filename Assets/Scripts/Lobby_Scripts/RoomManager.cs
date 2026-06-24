using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class RoomManager : MonoBehaviour
{
    [Header("--- UI Panels ---")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private GameObject panelRoom;
    [SerializeField] private GameObject panelJoin;
    [SerializeField] private GameObject panelSettings;

    [Header("--- Main Panel ---")]
    [SerializeField] private Button btnCreateRoom;
    [SerializeField] private Button btnJoinRoom;
    [SerializeField] private Button btnSettings;
    [SerializeField] private TMP_InputField inputFieldPlayerName;

    [Header("--- Room Panel ---")]
    [SerializeField] private TMP_Text textRoomCode;
    [SerializeField] private Button btnStartGame;
    [SerializeField] private Button btnLeaveRoom;

    [Header("--- Join Panel ---")]
    [SerializeField] private TMP_InputField inputFieldRoomCode;
    [SerializeField] private Button btnSubmitCode;
    [SerializeField] private Button btnCloseJoin;

    [Header("--- Settings Panel ---")]
    [SerializeField] private Button btnCloseSettings;

    [Header("--- 3D Player Stages ---")]
    [SerializeField] private GameObject[] playerStages = new GameObject[4];

    [Header("--- 4종 프리팹 직접 등록 (복사본) ---")]
    [SerializeField] private GameObject[] characterPrefabs = new GameObject[4];

    private string localPlayerName = "Player";
    private GameObject[] spawnedCharacters = new GameObject[4];

    private void Start()
    {
        ShowPanel(panelMain);

        btnCreateRoom.onClick.AddListener(OnCreateRoomClicked);
        btnJoinRoom.onClick.AddListener(() => ShowPanel(panelJoin));
        btnSettings.onClick.AddListener(() => ShowPanel(panelSettings));

        btnSubmitCode.onClick.AddListener(OnSubmitCodeClicked);
        btnCloseJoin.onClick.AddListener(() => ShowPanel(panelMain));
        btnCloseSettings.onClick.AddListener(() => ShowPanel(panelMain));

        btnLeaveRoom.onClick.AddListener(OnLeaveRoomClicked);
        btnStartGame.onClick.AddListener(OnStartGameClicked);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += UpdatePlayerSlotsUI;
            NetworkManager.Singleton.OnClientDisconnectCallback += UpdatePlayerSlotsUI;
        }

        ClearSpawnedCharacters();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= UpdatePlayerSlotsUI;
            NetworkManager.Singleton.OnClientDisconnectCallback -= UpdatePlayerSlotsUI;
        }
    }

    private void OnCreateRoomClicked()
    {
        SaveLocalNickname();

        if (NetworkManager.Singleton.StartHost())
        {
            ShowPanel(panelRoom);
            string randomCode = GenerateRandomRoomCode(6);
            textRoomCode.text = "CODE: " + randomCode;

            btnStartGame.gameObject.SetActive(true);
            UpdatePlayerSlotsUI(NetworkManager.Singleton.LocalClientId);
        }
    }

    private void OnSubmitCodeClicked()
    {
        SaveLocalNickname();
        string inputCode = inputFieldRoomCode.text.Trim().ToUpper();

        if (NetworkManager.Singleton.StartClient())
        {
            ShowPanel(panelRoom);
            textRoomCode.text = "접속 중인 방: " + (string.IsNullOrEmpty(inputCode) ? "DEFAULT" : inputCode);
            btnStartGame.gameObject.SetActive(false);
        }
    }

    private void OnLeaveRoomClicked()
    {
        NetworkManager.Singleton.Shutdown();
        ShowPanel(panelMain);
    }

    private void OnStartGameClicked()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowPanel(panelMain);
        }
    }

    private void UpdatePlayerSlotsUI(ulong clientId)
    {
        // 🌟 소유권 배정 스폰 권한은 오직 호스트(Server)만 가지고 제어해야 신뢰성이 보장됩니다.
        if (!NetworkManager.Singleton.IsServer) return;

        ClearSpawnedCharacters();

        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;

        for (int i = 0; i < connectedClients.Count; i++)
        {
            if (i < playerStages.Length && playerStages[i] != null)
            {
                if (i < characterPrefabs.Length && characterPrefabs[i] != null)
                {
                    GameObject characterInstance = Instantiate(
                        characterPrefabs[i],
                        playerStages[i].transform.position + new Vector3(0, 0.1f, 0),
                        playerStages[i].transform.rotation
                    );

                    // 🌟 [핵심 패치]: 넷코드 네트워크망에 이 캐릭터를 정식 스폰하되, 
                    // i번째 슬롯에 매칭되는 실제 유저의 고유 ClientId(`connectedClients[i]`)에게 소유권을 넘겨줍니다.
                    NetworkObject netObj = characterInstance.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.SpawnWithOwnership(connectedClients[i]);
                    }

                    spawnedCharacters[i] = characterInstance;

                    string displayName = (i == 0) ? localPlayerName : $"Player {i + 1}";
                    Create3DNameTagOnly(characterInstance, displayName);
                }
            }
        }
    }

    private void ClearSpawnedCharacters()
    {
        if (spawnedCharacters == null) return;

        for (int i = 0; i < spawnedCharacters.Length; i++)
        {
            if (spawnedCharacters[i] != null)
            {
                // 넷코드 관리 오브젝트는 서버가 Despawn으로 안전하게 네트워크 망에서 해제해야 유실이 없습니다.
                NetworkObject netObj = spawnedCharacters[i].GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                else if (spawnedCharacters[i] != null)
                {
                    Destroy(spawnedCharacters[i]);
                }
                spawnedCharacters[i] = null;
            }
        }
    }

    private void ShowPanel(GameObject panelToActivate)
    {
        panelMain.SetActive(panelToActivate == panelMain);
        panelRoom.SetActive(panelToActivate == panelRoom);
        panelJoin.SetActive(panelToActivate == panelJoin);
        panelSettings.SetActive(panelToActivate == panelSettings);

        if (panelToActivate != null && panelToActivate != panelRoom)
        {
            ClearSpawnedCharacters();
        }
        else
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                UpdatePlayerSlotsUI(NetworkManager.Singleton.LocalClientId);
            }
        }
    }

    private string GenerateRandomRoomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[length];
        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[Random.Range(0, chars.Length)];
        }
        return new string(stringChars);
    }

    private void SaveLocalNickname()
    {
        if (inputFieldPlayerName != null && !string.IsNullOrEmpty(inputFieldPlayerName.text))
        {
            localPlayerName = inputFieldPlayerName.text.Trim();
        }
        else
        {
            localPlayerName = "User_" + Random.Range(1000, 9999);
        }
    }

    private void Create3DNameTagOnly(GameObject character, string name)
    {
        GameObject uiAnchor = new GameObject("3D_UI_Anchor");
        uiAnchor.transform.SetParent(character.transform);
        uiAnchor.transform.localPosition = new Vector3(0, 2.1f, 0);
        uiAnchor.transform.localRotation = Quaternion.Euler(0, 180f, 0);

        TextMesh textMesh = uiAnchor.AddComponent<TextMesh>();
        textMesh.text = name;
        textMesh.fontSize = 45;
        textMesh.characterSize = 0.04f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
    }
}