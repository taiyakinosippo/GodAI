using System;

/// <summary>
/// Gemini APIから返ってくる神様の判定データ。
/// JsonUtility でデシリアライズするため [Serializable] が必須。
/// </summary>
[Serializable]
public class GodItemData
{
    /// <summary>落下させるアイテムの英語名（GameObjectの名前にも使う）</summary>
    public string itemName;

    /// <summary>Pollinations.ai に渡す画像生成プロンプト（英語）</summary>
    public string imagePrompt;

    /// <summary>スポーン時のオブジェクトスケール倍率</summary>
    public float scale;

    /// <summary>Rigidbody2D に設定する質量</summary>
    public float mass;

    /// <summary>画面に表示する神様のセリフ</summary>
    public string godMessage;
}
