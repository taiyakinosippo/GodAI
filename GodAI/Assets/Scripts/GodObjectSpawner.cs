using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Texture2D からスプライトを生成し、物理挙動付きの動的ゲームオブジェクトをスポーンするクラス。
/// </summary>
public class GodObjectSpawner : MonoBehaviour
{
    [Header("物理設定")]
    [Tooltip("Rigidbody2Dの重力スケール（1=標準）")]
    [SerializeField] private float gravityScale = 1f;

    [Tooltip("Rigidbody2Dの抵抗（空気抵抗）")]
    [SerializeField] private float linearDrag = 0.1f;

    [Header("コライダー設定")]
    [Tooltip("PolygonCollider2D の輪郭アルファ閾値（0〜1）")]
    [SerializeField] private float alphaThreshold = 0.1f;

    [Tooltip("輪郭検出の詳細度（0〜1、小さいほど粗い）")]
    [SerializeField] private float detail = 0.05f;

    /// <summary>
    /// 指定した Texture2D・GodItemData・スポーン位置からゲームオブジェクトを生成する。
    /// </summary>
    /// <param name="texture">Pollinations.ai から取得した Texture2D</param>
    /// <param name="itemData">Gemini API から取得した GodItemData</param>
    /// <param name="spawnPosition">スポーン地点（Worldスペース）</param>
    /// <returns>生成されたゲームオブジェクト</returns>
    public GameObject SpawnGodObject(Texture2D texture, GodItemData itemData, Vector3 spawnPosition)
    {
        // ---- 1. Sprite 生成 ----
        // PixelsPerUnit=100 固定、pivot=中心
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        // ---- 2. ゲームオブジェクト生成 ----
        GameObject obj = new GameObject(string.IsNullOrEmpty(itemData.itemName) ? "GodObject" : itemData.itemName);
        obj.transform.position   = spawnPosition;
        obj.transform.localScale = Vector3.one * Mathf.Max(0.1f, itemData.scale); // 最低0.1倍を保証

        // ---- 3. SpriteRenderer ----
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite      = sprite;
        sr.sortingOrder = 10; // UIより前面に表示

        // ---- 4. PolygonCollider2D（輪郭自動生成）----
        // Sprite.Create で生成した Sprite の物理形状を使って PolygonCollider2D を設定する
        PolygonCollider2D poly = obj.AddComponent<PolygonCollider2D>();
        ApplyPhysicsShape(poly, sprite, texture);

        // ---- 5. Rigidbody2D ----
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.mass                   = Mathf.Max(0.1f, itemData.mass); // 最低0.1kgを保証
        rb.gravityScale           = gravityScale;
        rb.linearDamping          = linearDrag;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation          = RigidbodyInterpolation2D.Interpolate;

        Debug.Log($"[GodObjectSpawner] スポーン: {itemData.itemName} / scale={itemData.scale} / mass={itemData.mass}kg");
        return obj;
    }

    /// <summary>
    /// Sprite の物理形状（physicsShape）を PolygonCollider2D に適用する。
    /// 物理形状が空の場合は Texture2D からアルファ輪郭を手動生成する。
    /// </summary>
    private void ApplyPhysicsShape(PolygonCollider2D poly, Sprite sprite, Texture2D texture)
    {
        int pathCount = sprite.GetPhysicsShapeCount();
        if (pathCount > 0)
        {
            // Sprite.Create で物理形状が自動生成されている場合はそれをそのまま使う
            poly.pathCount = pathCount;
            var path = new List<Vector2>();
            for (int i = 0; i < pathCount; i++)
            {
                path.Clear();
                sprite.GetPhysicsShape(i, path);
                poly.SetPath(i, path.ToArray());
            }
        }
        else
        {
            // フォールバック: テクスチャのアルファ閾値から単純な矩形を生成
            Debug.LogWarning("[GodObjectSpawner] 物理形状なし → 矩形コライダーにフォールバック");
            float halfW = (texture.width  / 100f) * 0.5f;
            float halfH = (texture.height / 100f) * 0.5f;
            poly.pathCount = 1;
            poly.SetPath(0, new Vector2[]
            {
                new Vector2(-halfW, -halfH),
                new Vector2( halfW, -halfH),
                new Vector2( halfW,  halfH),
                new Vector2(-halfW,  halfH),
            });
        }
    }
}
