using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cloud Function Proxy 経由で Gemini API へリクエストを送るクラス。
/// APIキーはサーバー側（環境変数）にのみ存在し、このスクリプトには含まれない。
/// </summary>
public class GeminiRequester : MonoBehaviour
{
    /// <summary>
    /// Cloud Function のデプロイ後に発行される URL。
    /// 例: https://asia-northeast1-godai-proxy-123456.cloudfunctions.net/gemini-proxy
    /// GodManager の Inspector から設定する。
    /// </summary>
    [HideInInspector] public string proxyUrl;

    /// <summary>
    /// プレイヤーの願いを Proxy サーバーへ送信し、GodItemData を返す。
    /// 失敗時は null を返す。
    /// </summary>
    /// <param name="playerWish">プレイヤーが入力した願い</param>
    /// <param name="apiKey">未使用（Proxy移行後は不要。互換性のため残す）</param>
    public async Task<GodItemData> RequestGodResponseAsync(string playerWish, string apiKey = "")
    {
        if (string.IsNullOrEmpty(proxyUrl))
        {
            Debug.LogError("[GeminiRequester] proxyUrl が設定されていません。GodManager の Inspector で設定してください。");
            return null;
        }

        // Proxy へ送るのは「願い」テキストだけ（APIキーは不要）
        string jsonPayload = "{\"wish\":\"" + EscapeJson(playerWish) + "\"}";

        Debug.Log("[GeminiRequester] Proxy へリクエスト: " + proxyUrl);

        using (UnityWebRequest request = new UnityWebRequest(proxyUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60; // 60秒タイムアウト

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[GeminiRequester] Proxy エラー: " + request.error +
                               "\nResponse: " + request.downloadHandler.text);
                return null;
            }

            string responseJson = request.downloadHandler.text;
            Debug.Log("[GeminiRequester] Proxy レスポンス: " + responseJson);

            // Proxy は検証済みの GodItemData JSON を直接返す
            GodItemData itemData = JsonUtility.FromJson<GodItemData>(responseJson);
            if (itemData == null)
            {
                Debug.LogError("[GeminiRequester] JSON デシリアライズ失敗: " + responseJson);
            }
            return itemData;
        }
    }

    /// <summary>
    /// JSON 文字列として安全にエスケープする。
    /// </summary>
    private string EscapeJson(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
