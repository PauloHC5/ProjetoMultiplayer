using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Script de TESTE para Photon PUN2.
/// Coloque este componente em um GameObject vazio dentro de uma scene de teste.
/// Ele vai:
///   1) Conectar ao Photon automaticamente (ConnectUsingSettings);
///   2) Entrar no Lobby padrão;
///   3) Criar (ou entrar, se já existir) uma sala com nome fixo/configurável,
///      visível e aberta, para aparecer na lista de lobby que você está montando.
///
/// Requisitos:
///   - Pacote "Photon PUN2" instalado (Asset Store / Package Manager);
///   - PhotonServerSettings configurado com seu AppId (Photon Dashboard).
/// </summary>
public class PhotonTestConnector : MonoBehaviourPunCallbacks
{
    [Header("Configuração da Sala de Teste")]
    [Tooltip("Nome da sala. Deixe fixo se quiser sempre reconectar na mesma sala de teste.")]
    [SerializeField] private string roomName = "SalaTeste_AedesInvasion";

    [Tooltip("Número máximo de jogadores na sala (conforme GDD: até 3).")]
    [SerializeField] private byte maxPlayers = 3;

    [Tooltip("Se true, gera um nickname aleatório a cada execução (útil pra simular múltiplos clientes).")]
    [SerializeField] private bool useRandomNickname = true;

    [Tooltip("Nome de sala único por execução (bom pra testar SE a sala aparece na lista, sem conflitar).")]
    [SerializeField] private bool useUniqueRoomName = false;

    private void Start()
    {
        Debug.Log("[PhotonTest] Iniciando conexão com o Photon Master Server...");

        // Nickname (Photon exige um nome de player pra alguns fluxos)
        PhotonNetwork.NickName = useRandomNickname
            ? "TestPlayer_" + Random.Range(1000, 9999)
            : "TestPlayer";

        // Usa as configs do PhotonServerSettings (Resources/PhotonServerSettings)
        PhotonNetwork.AutomaticallySyncScene = true; // útil se depois for sincronizar troca de scene
        PhotonNetwork.ConnectUsingSettings();
    }

    // ------------------------------------------------------------------
    // CALLBACKS DE CONEXÃO
    // ------------------------------------------------------------------

    public override void OnConnectedToMaster()
    {
        Debug.Log($"[PhotonTest] Conectado ao Master Server. Região: {PhotonNetwork.CloudRegion}");
        Debug.Log("[PhotonTest] Entrando no Lobby...");
        PhotonNetwork.JoinLobby(); // lobby padrão (TypedLobby.Default)
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[PhotonTest] Desconectado do Photon. Motivo: {cause}");
    }

    // ------------------------------------------------------------------
    // CALLBACKS DE LOBBY
    // ------------------------------------------------------------------

    public override void OnJoinedLobby()
    {
        Debug.Log("[PhotonTest] Entrou no Lobby com sucesso.");

        string finalRoomName = useUniqueRoomName
            ? $"{roomName}_{System.Guid.NewGuid().ToString().Substring(0, 5)}"
            : roomName;

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,   // <- crucial para aparecer na lista do lobby
            IsOpen = true,      // <- permite outros jogadores entrarem
            // CustomRoomPropertiesForLobby pode ser usado depois se você
            // quiser mostrar dados extras na lista (ex: "Fase", "Modo", etc.)
        };

        Debug.Log($"[PhotonTest] Tentando criar/entrar na sala '{finalRoomName}'...");
        PhotonNetwork.JoinOrCreateRoom(finalRoomName, options, TypedLobby.Default);
    }

    // ------------------------------------------------------------------
    // CALLBACKS DE SALA
    // ------------------------------------------------------------------

    public override void OnJoinedRoom()
    {
        Debug.Log($"[PhotonTest] Entrou/criou a sala '{PhotonNetwork.CurrentRoom.Name}' " +
                  $"({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers} jogadores).");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonTest] Falha ao criar sala: {message} (code {returnCode})");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[PhotonTest] Falha ao entrar na sala: {message} (code {returnCode})");
    }

    public override void OnRoomListUpdate(System.Collections.Generic.List<RoomInfo> roomList)
    {
        // Só pra debug: confirma que a sala está sendo listada pelo Master Server.
        Debug.Log($"[PhotonTest] Lista de salas atualizada ({roomList.Count} salas):");
        foreach (var room in roomList)
        {
            Debug.Log($"   - {room.Name} | Visível: {room.IsVisible} | Aberta: {room.IsOpen} | " +
                      $"{room.PlayerCount}/{room.MaxPlayers}");
        }
    }
}