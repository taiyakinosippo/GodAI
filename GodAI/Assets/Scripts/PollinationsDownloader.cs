using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Pollinations.ai からドット絵を取得し、白背景を除去して透過Texture2Dを返すクラス。
/// </summary>
public class PollinationsDownloader : MonoBehaviour
{
    private const string BaseUrl = "https://image.pollinations.ai/prompt/";

    [Header("白背景除去設定")]
    [Tooltip("この輝度以上かつ彩度が低い画素を透明にする（0〜1）")]
    [SerializeField] [Range(0.5f, 1.0f)] private float whiteThreshold = 0.88f;

    // GodManager から URL を受け取るフィールド（互換性維持、未使用）
    [HideInInspector] public string imageProxyUrl;

    // ---- Public API -------------------------------------------------------

    /// <summary>
    /// imagePrompt からドット絵Texture2Dを取得する（白背景は透過済み）。
    /// 失敗時は最大2回リトライ。それでも失敗ならnullを返す。
    /// </summary>
    public async Task<Texture2D> DownloadTextureAsync(string imagePrompt, int width = 512, int height = 512)
    {
        // カンマはエンコードしない（Pollinations.aiの仕様）
        string encoded = UnityWebRequest.EscapeURL(imagePrompt)
            .Replace("%2c", ",").Replace("%2C", ",");

        // pixel art 専用パラメータ（model指定なし＝デフォルトflux）
        string url = $"{BaseUrl}{encoded}?width={width}&height={height}&nologo=true&enhance=false";

        Debug.Log("[PollinationsDownloader] 画像リクエスト URL: " + url.Substring(0, Math.Min(120, url.Length)) + "...");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            if (attempt > 1)
            {
                int wait = attempt == 2 ? 3000 : 6000;
                Debug.Log($"[PollinationsDownloader] {wait / 1000}秒後にリトライ ({attempt}/3)...");
                await Task.Delay(wait);
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 120;
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D raw = DownloadHandlerTexture.GetContent(request);
                    Debug.Log($"[PollinationsDownloader] 画像取得成功: {raw.width}x{raw.height} → 白背景除去中...");
                    Texture2D transparent = RemoveWhiteBackground(raw);
                    return transparent;
                }

                Debug.LogWarning($"[PollinationsDownloader] 試行{attempt}/3 失敗: " + request.error);
            }
        }

        Debug.LogError("[PollinationsDownloader] 3回試行しましたが画像取得に失敗しました。");
        return null;
    }

    // ---- 白背景除去 -------------------------------------------------------

    /// <summary>
    /// 白背景（高輝度・低彩度）のピクセルを透明にして透過Texture2Dを生成する。
    /// ピクセルアートの輪郭も自然に残る。
    /// </summary>
    private Texture2D RemoveWhiteBackground(Texture2D source)
    {
        int w = source.width;
        int h = source.height;
        Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);

        Color[] pixels;
        try
        {
            pixels = source.GetPixels();
        }
        catch
        {
            // 読み取り不可の場合はRenderTextureを経由する
            RenderTexture rt = RenderTexture.GetTemporary(w, h);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(w, h);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            pixels = readable.GetPixels();
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float brightness = (c.r + c.g + c.b) / 3f;
            float saturation = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);

            // 白・近白（高輝度・低彩度）を透明に
            if (brightness >= whiteThreshold && saturation < 0.15f)
            {
                float alpha = 1f - Mathf.Clamp01(
                    (brightness - whiteThreshold) / (1f - whiteThreshold + 0.001f));
                pixels[i] = new Color(c.r, c.g, c.b, alpha < 0.25f ? 0f : alpha);
            }
            // それ以外は不透明のまま
        }

        result.SetPixels(pixels);
        result.Apply();
        Debug.Log("[PollinationsDownloader] 白背景除去完了 → 透過Texture2D生成");
        return result;
    }
}
