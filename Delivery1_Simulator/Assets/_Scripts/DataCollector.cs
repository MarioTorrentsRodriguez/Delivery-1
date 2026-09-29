using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Puente entre el Simulator y el servidor:
///  1. Escucha los eventos del Simulator (nuevo jugador, sesión, compra...).
///  2. Los envía al servidor mediante ApiClient.
///  3. Cuando el servidor responde con el id, avisa al Simulator (CallbackEvents)
///     para que continúe la simulación.
/// Poner este componente en el mismo GameObject que el Simulator.
/// </summary>
[RequireComponent(typeof(ApiClient))]
public class DataCollector : MonoBehaviour
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    // El Simulator entrega el género como float 0..1; aquí decidimos cómo se traduce.
    private const float MaleThreshold = 0.45f;   // [0, 0.45)    -> M
    private const float FemaleThreshold = 0.90f; // [0.45, 0.90) -> F, resto -> O

    private ApiClient _api;

    // El Simulator, al cerrar una sesión, espera que le devolvamos el playerId.
    private readonly Dictionary<uint, uint> _sessionToPlayer = new Dictionary<uint, uint>();

    private void Awake()
    {
        _api = GetComponent<ApiClient>();
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
        _api.Post("new_player.php", fields, playerId => CallbackEvents.OnAddPlayerCallback?.Invoke(playerId));
    }

    private void HandleNewSession(DateTime date, uint playerId)
    {
        var fields = new Dictionary<string, string>
        {
            { "player_id", playerId.ToString() },
            { "date", Format(date) }
        };
        _api.Post("start_session.php", fields, sessionId =>
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
        _api.Post("buy_item.php", fields, _ => CallbackEvents.OnItemBuyCallback?.Invoke(sessionId));
    }

    private void HandleEndSession(DateTime date, uint sessionId)
    {
        var fields = new Dictionary<string, string>
        {
            { "session_id", sessionId.ToString() },
            { "date", Format(date) }
        };
        _api.Post("end_session.php", fields, _ =>
        {
            uint playerId = _sessionToPlayer[sessionId];
            _sessionToPlayer.Remove(sessionId); // evita que el diccionario crezca sin límite
            CallbackEvents.OnEndSessionCallback?.Invoke(playerId);
        });
    }

    private static string Format(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string GenderToCode(float g) => g < MaleThreshold ? "M" : g < FemaleThreshold ? "F" : "O";
}
