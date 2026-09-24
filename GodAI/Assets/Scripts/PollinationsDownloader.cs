using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Proxy経由でHugging FaceのPixel Artモデルから画像を取得し、
/// 白背景を除去して透過Texture2Dを返すクラス。
/// </summary>
public class PollinationsDownloader : MonoBehaviour
{
    [Header("Image Proxy 設定")]
    [Tooltip("Vercel の image-proxy エンドポイント URL")]
    [HideInInspector] public string imageProxyUrl;

    [Header("白背景除去設定")]
    [Tooltip("この輝度以上かつ彩度が低い画素を透明にする（0〜1）")]
    [SerializeField] [Range(0.5f, 1.0f)] private float whiteThreshold = 0.88f;

    [Header("フォールバック（Proxy不使用時）")]
    [Tooltip("直接Pollinations.aiを使う（デバッグ用）")]
    [SerializeField] private bool useFallbackDirect = false;

    // ---- Public API -------------------------------------------------------

    /// <summary>
    /// imagePrompt からドット絵Texture2Dを取得する（白背景は透過済み）。
    /// 失敗時はnullを返す。
    /// </summary>
    public async Task<Texture2D> DownloadTextureAsync(string imagePrompt, int width = 512, int height = 512)
    {
        if (useFallbackDirect || string.IsNullOrEmpty(imageProxyUrl))
            return await FallbackPollinationsAsync(imagePrompt);

        return await DownloadViaProxyAsync(imagePrompt);
    }

    // ---- Private: Proxy経由 -----------------------------------------------

    private async Task<Texture2D> DownloadViaProxyAsync(string imagePrompt)
    {
        Debug.Log("[ImageDownloader] image-proxy へリクエスト: " + imagePrompt.Substring(0, Math.Min(60, imagePrompt.Length)));

        string jsonBody = "{\"prompt\":\"" + EscapeJson(imagePrompt) + "\"}";

        using (UnityWebRequest req = new UnityWebRequest(imageProxyUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ImageDownloader] Proxy エラー: " + req.error);
                return null;
            }

            string json = req.downloadHandler.text;
            Debug.Log("[ImageDownloader] Proxy レスポンス source: " + ExtractField(json, "source"));

            // Base64画像が返ってきた場合
            string base64 = ExtractField(json, "imageBase64");
            if (!string.IsNullOrEmpty(base64))
            {
                Texture2D tex = Base64ToTexture(base64);
                if (tex != null)
                {
                    Debug.Log("[ImageDownloader] Base64画像取得成功 → 白背景除去中");
                    return RemoveWhiteBackground(tex);
                }
            }

            // URLが返ってきた場合（Pollinationsフォールバック）
            string imageUrl = ExtractField(json, "imageUrl");
            if (!string.IsNullOrEmpty(imageUrl))
            {
                Debug.Log("[ImageDownloader] Pollinationsフォールバック URL: " + imageUrl);
                return await DownloadFromUrlAsync(imageUrl);
            }

            Debug.LogError("[ImageDownloader] imageBase64 も imageUrl も取得できませんでした");
            return null;
        }
    }

    // ---- Private: URL直接ダウンロード -------------------------------------

    private async Task<Texture2D> FallbackPollinationsAsync(string imagePrompt)
    {
        string encoded = UnityWebRequest.EscapeURL(imagePrompt).Replace("%2c", ",").Replace("%2C", ",");
        string url = $"https://image.pollinations.ai/prompt/{encoded}?width=512&height=512&nologo=true";
        Debug.Log("[ImageDownloader] Pollinations直接: " + url);
        return await DownloadFromUrlAsync(url);
    }

    private async Task<Texture2D> DownloadFromUrlAsync(string url)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            if (attempt > 1) { await Task.Delay(3000); Debug.Log("[ImageDownloader] リトライ..."); }

            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
            {
                req.timeout = 120;
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(req);
                    Debug.Log($"[ImageDownloader] 画像取得成功: {tex.width}x{tex.height}");
                    return RemoveWhiteBackground(tex);
                }
                Debug.LogWarning($"[ImageDownloader] 試行{attempt}失敗: " + req.error);
            }
        }
        Debug.LogError("[ImageDownloader] 画像取得を2回試みましたが失敗しました");
        return null;
    }

    // ---- 白背景除去 -------------------------------------------------------

    /// <summary>
    /// 白背景（高輝度・低彩度）のピクセルを透明にして透過Textureを生成する。
    /// </summary>
    private Texture2D RemoveWhiteBackground(Texture2D source)
    {
        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color[] pixels;

        try { pixels = source.GetPixels(); }
        catch
        {
            // 読み取り不可の場合はRenderTextureを経由して読み取り可能にする
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(source.width, source.height);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            pixels = readable.GetPixels();
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float brightness  = (c.r + c.g + c.b) / 3f;
            float saturation  = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);

            if (brightness >= whiteThreshold && saturation < 0.15f)
            {
                // 白に近いほど完全透明に、境界はスムーズに
                float alpha = 1f - Mathf.Clamp01((brightness - whiteThreshold) / (1f - whiteThreshold + 0.001f));
                pixels[i] = new Color(c.r, c.g, c.b, alpha < 0.3f ? 0f : alpha);
            }
        }

        result.SetPixels(pixels);
        result.Apply();
        Debug.Log("[ImageDownloader] 白背景除去完了");
        return result;
    }

    // ---- ユーティリティ ---------------------------------------------------

    private Texture2D Base64ToTexture(string base64)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes)) return tex;
            Debug.LogError("[ImageDownloader] Base64 → Texture変換失敗");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError("[ImageDownloader] Base64デコードエラー: " + e.Message);
            return null;
        }
    }

    /// <summary>JSONから特定フィールドの文字列値を簡易抽出する</summary>
    private string ExtractField(string json, string fieldName)
    {
        string key = $"\"{fieldName}\":\"";
        int idx = json.IndexOf(key);
        if (idx == -1) return string.Empty;
        int start = idx + key.Length;
        int end = json.IndexOf("\"", start);
        if (end == -1) return string.Empty;
        return json.Substring(start, end - start);
    }

    private string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
}
