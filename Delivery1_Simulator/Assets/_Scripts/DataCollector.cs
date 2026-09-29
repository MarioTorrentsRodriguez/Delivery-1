using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Puente entre el Simulator y el destino de los datos:
///  1. Escucha los eventos del Simulator (nuevo jugador, sesión, compra...).
///  2. Los entrega al destino elegido en "Mode":
///       - Console: los imprime en la consola de Unity (IDs inventados en local).
///       - Server : los envía por HTTP mediante ApiClient (IDs los da el servidor).
///  3. Avisa al Simulator con CallbackEvents para que continúe la simulación.
/// Poner este componente en el mismo GameObject que el Simulator.
/// </summary>
public class DataCollector : MonoBehaviour
{
    public enum OutputMode { Console, Server }

    [SerializeField] private OutputMode mode = OutputMode.Console;
    [Tooltip("Solo modo Console: cuántos eventos se procesan por frame (más = más rápido).")]
    [SerializeField] private int consoleEventsPerFrame = 200;

    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    // El Simulator entrega el género como float 0..1; aquí decidimos cómo se traduce.
    private const float MaleThreshold = 0.45f;   // [0, 0.45)    -> M
    private const float FemaleThreshold = 0.90f; // [0.45, 0.90) -> F, resto -> O

    private ApiClient _api;

    // El Simulator, al cerrar una sesión, espera que le devolvamos el playerId.
    private readonly Dictionary<uint, uint> _sessionToPlayer = new Dictionary<uint, uint>();

    // Solo modo Console: contadores de IDs y cola de respuestas pendientes.
    // La cola evita llamadas recursivas gigantes (el Simulator encadena eventos).
    private readonly Dictionary<string, uint> _localIds = new Dictionary<string, uint>();
    private readonly Queue<Action> _pendingReplies = new Queue<Action>();

    private void Awake()
    {
        if (mode == OutputMode.Server)
        {
            _api = GetComponent<ApiClient>();
            if (_api == null)
                _api = gameObject.AddComponent<ApiClient>();
        }
    }

    private void OnEnable()
    {
        Simulator.OnNewPlayer += HandleNewPlayer;
        Simulator.OnNewSession += HandleNewSession;
        Simulator.OnEndSession += HandleEndSession;
        Simulator.OnBuyItem += HandleBuyItem;
    }

    private void OnDisable()
    {
        Simulator.OnNewPlayer -= HandleNewPlayer;
        Simulator.OnNewSession -= HandleNewSession;
        Simulator.OnEndSession -= HandleEndSession;
        Simulator.OnBuyItem -= HandleBuyItem;
    }

    // Modo Console: devuelve las respuestas al Simulator poco a poco, sin recursión.
    private void Update()
    {
        int processed = 0;
        while (_pendingReplies.Count > 0 && processed < consoleEventsPerFrame)
        {
            _pendingReplies.Dequeue().Invoke();
            processed++;
        }
    }

    #region Simulator events

    private void HandleNewPlayer(string name, string country, int age, float gender, DateTime date)
    {
        var fields = new Dictionary<string, string>
        {
            { "name", name },
            { "country", country },
            { "age", age.ToString(CultureInfo.InvariantCulture) },
            { "gender", GenderToCode(gender) },
            { "date", Format(date) }
        };
        Send("new_player.php", fields, playerId => CallbackEvents.OnAddPlayerCallback?.Invoke(playerId));
    }

    private void HandleNewSession(DateTime date, uint playerId)
    {
        var fields = new Dictionary<string, string>
        {
            { "player_id", playerId.ToString() },
            { "date", Format(date) }
        };
        Send("start_session.php", fields, sessionId =>
        {
            _sessionToPlayer[sessionId] = playerId;
            CallbackEvents.OnNewSessionCallback?.Invoke(sessionId);
        });
    }

    private void HandleBuyItem(int itemId, DateTime date, uint sessionId)
    {
        var fields = new Dictionary<string, string>
        {
            { "session_id", sessionId.ToString() },
            { "item_id", itemId.ToString(CultureInfo.InvariantCulture) },
            { "date", Format(date) }
        };
        Send("buy_item.php", fields, _ => CallbackEvents.OnItemBuyCallback?.Invoke(sessionId));
    }

    private void HandleEndSession(DateTime date, uint sessionId)
    {
        var fields = new Dictionary<string, string>
        {
            { "session_id", sessionId.ToString() },
            { "date", Format(date) }
        };
        Send("end_session.php", fields, _ =>
        {
            uint playerId = _sessionToPlayer[sessionId];
            _sessionToPlayer.Remove(sessionId); // evita que el diccionario crezca sin límite
            CallbackEvents.OnEndSessionCallback?.Invoke(playerId);
        });
    }

    #endregion

    #region Output (Console / Server)

    /// <summary>Entrega un evento al destino elegido y, cuando hay id, llama a onId.</summary>
    private void Send(string endpoint, Dictionary<string, string> fields, Action<uint> onId)
    {
        if (mode == OutputMode.Server)
        {
            _api.Post(endpoint, fields, onId);
            return;
        }

        // Modo Console: id local + imprimir + responder en el siguiente Update
        uint id = NextLocalId(endpoint);
        Debug.Log(FormatLog(endpoint, id, fields));
        _pendingReplies.Enqueue(() => onId(id));
    }

    private uint NextLocalId(string endpoint)
    {
        _localIds.TryGetValue(endpoint, out uint last);
        uint next = last + 1;
        _localIds[endpoint] = next;
        return next;
    }

    private static string FormatLog(string endpoint, uint id, Dictionary<string, string> fields)
    {
        string label = endpoint.Replace(".php", "").ToUpperInvariant();
        var sb = new StringBuilder($"[{label}] id={id}");
        foreach (KeyValuePair<string, string> f in fields)
            sb.Append($" | {f.Key}={f.Value}");
        return sb.ToString();
    }

    #endregion

    private static string Format(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string GenderToCode(float g) => g < MaleThreshold ? "M" : g < FemaleThreshold ? "F" : "O";
}