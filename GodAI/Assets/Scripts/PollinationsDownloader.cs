using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Pollinations.ai から imagePrompt に対応した画像をダウンロードし、Texture2D として返すクラス。
/// </summary>
public class PollinationsDownloader : MonoBehaviour
{
    private const string BaseUrl = "https://image.pollinations.ai/prompt/";

    /// <summary>
    /// imagePrompt をURLエンコードして Pollinations.ai へ GET リクエストを送り、
    /// Texture2D を返す。失敗時は null を返す。
    /// </summary>
    /// <param name="imagePrompt">画像生成プロンプト（英語）</param>
    /// <param name="width">リクエスト解像度（幅）</param>
    /// <param name="height">リクエスト解像度（高さ）</param>
    public async Task<Texture2D> DownloadTextureAsync(string imagePrompt, int width = 512, int height = 512)
    {
        // URLエンコード
        string encodedPrompt = UnityWebRequest.EscapeURL(imagePrompt);

        // width / height パラメータで生成サイズを指定（正方形推奨）
        string url = $"{BaseUrl}{encodedPrompt}?width={width}&height={height}&nologo=true&model=flux";

        Debug.Log("[PollinationsDownloader] 画像リクエスト URL: " + url);

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            // タイムアウト設定（画像生成には時間がかかる場合がある）
            request.timeout = 120;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[PollinationsDownloader] 画像取得エラー: " + request.error +
                               "\nURL: " + url);
                return null;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Debug.Log($"[PollinationsDownloader] 画像取得成功: {texture.width}x{texture.height}");
            return texture;
        }
    }
}
