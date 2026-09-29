using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Responsabilidad única: enviar peticiones POST al servidor de forma ASÍNCRONA
/// (corrutinas + UnityWebRequest, nunca bloquea el hilo principal) y devolver el id
/// que responde el servidor. No sabe nada del juego ni del simulador.
/// </summary>
public class ApiClient : MonoBehaviour
{
    [SerializeField] private string baseUrl = "http://localhost/game/";
    [SerializeField] private int maxRetries = 5;
    [SerializeField] private float retryDelaySeconds = 1f;
    [SerializeField] private int timeoutSeconds = 10;

    [Serializable]
    private class ServerResponse
    {
        public long id;
        public string error;
    }

    /// <summary>Envía el POST y, si va bien, llama a onSuccess con el id devuelto.</summary>
    public void Post(string endpoint, Dictionary<string, string> fields, Action<uint> onSuccess)
    {
        StartCoroutine(PostRoutine(endpoint, fields, onSuccess));
    }

    private IEnumerator PostRoutine(string endpoint, Dictionary<string, string> fields, Action<uint> onSuccess)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            WWWForm form = new WWWForm();
            foreach (KeyValuePair<string, string> f in fields)
                form.AddField(f.Key, f.Value);

            using (UnityWebRequest request = UnityWebRequest.Post(baseUrl + endpoint, form))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    ServerResponse response = JsonUtility.FromJson<ServerResponse>(request.downloadHandler.text);
                    if (response != null && string.IsNullOrEmpty(response.error))
                    {
                        onSuccess?.Invoke((uint)response.id);
                        yield break;
                    }
                    Debug.LogError($"[ApiClient] {endpoint} rejected: {request.downloadHandler.text}");
                    yield break;
                }

                Debug.LogWarning($"[ApiClient] {endpoint} failed (attempt {attempt}/{maxRetries}): {request.error}");
            }

            yield return new WaitForSeconds(retryDelaySeconds);
        }

        Debug.LogError($"[ApiClient] {endpoint}: giving up after {maxRetries} attempts.");
    }
}