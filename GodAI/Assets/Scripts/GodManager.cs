using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 願い入力 → Gemini思考 → 画像生成 → オブジェクトスポーン → メッセージ表示
/// の一連の非同期パイプラインを統括するコントローラー。
///
/// [シーンセットアップ手順]
/// 1. 空のGameObject「GodManager」を作成し、このスクリプトをアタッチ。
/// 2. 同オブジェクトに GeminiRequester, PollinationsDownloader, GodObjectSpawner もアタッチ。
/// 3. InspectorでAPIキーと各UIコンポーネント・spawnPointを設定。
/// </summary>
public class GodManager : MonoBehaviour
{
    [Header("Proxy設定")]
    [Tooltip("Cloud Functions のデプロイ後に発行される URL\n例: https://asia-northeast1-xxx.cloudfunctions.net/gemini-proxy")]
    [SerializeField] private string proxyUrl = "https://god-ai-blush.vercel.app/api/gemini-proxy";

    [Header("依存コンポーネント")]
    [SerializeField] private GeminiRequester       geminiRequester;
    [SerializeField] private PollinationsDownloader pollinationsDownloader;
    [SerializeField] private GodObjectSpawner       godObjectSpawner;

    [Header("UI参照")]
    [Tooltip("願い事を入力するInputField")]
    [SerializeField] private InputField inputWish;

    [Tooltip("神様のメッセージを表示するTextMeshPro")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("送信ボタン")]
    [SerializeField] private Button submitButton;

    [Header("スポーン設定")]
    [Tooltip("オブジェクトが出現する座標（画面上空を設定推奨）")]
    [SerializeField] private Transform spawnPoint;

    // ---- ライフサイクル --------------------------------------------------------

    private void Start()
    {
        ValidateReferences();

        // GeminiRequester に Proxy URL を渡す
        if (geminiRequester != null)
            geminiRequester.proxyUrl = proxyUrl;

        // ボタンに非同期リスナーを登録
        submitButton.onClick.AddListener(async () => await ExecuteGodMiracle());
    }

    // ---- パブリックメソッド ----------------------------------------------------

    /// <summary>
    /// 願い入力から物体落下までの全パイプラインを実行する。
    /// </summary>
    public async Task ExecuteGodMiracle()
    {
        // ---- 入力バリデーション ----
        string wish = inputWish.text.Trim();
        if (string.IsNullOrEmpty(wish))
        {
            SetMessage("願いを入力してください……");
            return;
        }

        SetButtonInteractable(false);

        // ==============================
        // STEP 1: Gemini に思考させる
        // ==============================
        SetMessage("神様が思考中……");
        Debug.Log("[GodManager] STEP1: Geminiへリクエスト送信");

        GodItemData itemData = await geminiRequester.RequestGodResponseAsync(wish);

        if (itemData == null)
        {
            SetMessage("神様は沈黙した……（通信エラー）");
            SetButtonInteractable(true);
            return;
        }

        Debug.Log($"[GodManager] Gemini応答: item={itemData.itemName}, scale={itemData.scale}, mass={itemData.mass}");

        // ==============================
        // STEP 2: 画像をダウンロード
        // ==============================
        SetMessage($"神様が「{itemData.itemName}」を召喚中……");
        Debug.Log("[GodManager] STEP2: Pollinations.ai へ画像リクエスト");

        Texture2D texture = await pollinationsDownloader.DownloadTextureAsync(itemData.imagePrompt);

        if (texture == null)
        {
            SetMessage("神様の力が届かなかった……（画像取得エラー）");
            SetButtonInteractable(true);
            return;
        }

        // ==============================
        // STEP 3: オブジェクトをスポーン
        // ==============================
        Debug.Log("[GodManager] STEP3: オブジェクトスポーン");

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : new Vector3(0f, 8f, 0f);
        godObjectSpawner.SpawnGodObject(texture, itemData, spawnPos);

        // ==============================
        // STEP 4: 神様のセリフを表示
        // ==============================
        SetMessage("神様: 「" + itemData.godMessage + "」");
        Debug.Log("[GodManager] 全パイプライン完了");

        SetButtonInteractable(true);
    }

    // ---- プライベートヘルパー --------------------------------------------------

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
        Debug.Log("[GodManager] Message: " + text);
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (submitButton != null) submitButton.interactable = interactable;
    }

    /// <summary>
    /// Inspector参照の不備を早期検出してエラーログを出す。
    /// </summary>
    private void ValidateReferences()
    {
        if (geminiRequester       == null) Debug.LogError("[GodManager] GeminiRequester が未設定です。");
        if (pollinationsDownloader == null) Debug.LogError("[GodManager] PollinationsDownloader が未設定です。");
        if (godObjectSpawner      == null) Debug.LogError("[GodManager] GodObjectSpawner が未設定です。");
        if (inputWish             == null) Debug.LogError("[GodManager] inputWish (InputField) が未設定です。");
        if (messageText           == null) Debug.LogError("[GodManager] messageText (TextMeshProUGUI) が未設定です。");
        if (submitButton          == null) Debug.LogError("[GodManager] submitButton (Button) が未設定です。");
        if (spawnPoint            == null) Debug.LogWarning("[GodManager] spawnPoint が未設定です。デフォルト座標(0,8,0)を使用します。");

        if (string.IsNullOrEmpty(proxyUrl))
            Debug.LogError("[GodManager] proxyUrl が設定されていません！Cloud Functions デプロイ後に Inspector で URL を設定してください。");
    }
}
