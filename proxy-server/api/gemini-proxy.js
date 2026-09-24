/**
 * GodAI — Gemini API Proxy (Vercel Serverless Function)
 * 
 * Unity から { "wish": "願いテキスト" } を受け取り、
 * Gemini API へ転送して GodItemData JSON を返す。
 * APIキーは Vercel の環境変数 GEMINI_API_KEY にのみ保管。
 */
module.exports = async function handler(req, res) {
  // CORS ヘッダ（Unityからのリクエスト対応）
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  // プリフライトリクエスト対応
  if (req.method === 'OPTIONS') {
    return res.status(204).end();
  }

  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method Not Allowed' });
  }

  // 環境変数からAPIキーを取得（コードには絶対に書かない）
  const apiKey = process.env.GEMINI_API_KEY;
  if (!apiKey) {
    console.error('GEMINI_API_KEY が環境変数に設定されていません');
    return res.status(500).json({ error: 'Server configuration error' });
  }

  const { wish } = req.body;
  if (!wish || typeof wish !== 'string') {
    return res.status(400).json({ error: '`wish` フィールドが必要です' });
  }

  const systemInstruction =
    'あなたは理不尽でスケールが暴走した神様です。' +
    'プレイヤーの願いに対し、斜め上の解釈で解決する巨大な物体を考えてください。\n' +
    '出力は必ず次のJSONフォーマットのみとし、コードブロック(```)や前後の余計な文章は一切含めないでください。\n' +
    '{"itemName":"英語名","imagePrompt":"white background, 2d game sprite, single object, 英語プロンプト",' +
    '"scale":2.0,"mass":30.0,"godMessage":"神様のセリフ（日本語可）"}';

  const geminiUrl =
    `https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash:generateContent?key=${apiKey}`;

  const payload = {
    system_instruction: { parts: [{ text: systemInstruction }] },
    contents: [{ parts: [{ text: `願い: ${wish}` }] }]
  };

  try {
    const response = await fetch(geminiUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(55000)
    });

    if (!response.ok) {
      const errorText = await response.text();
      console.error('Gemini API エラー:', response.status, errorText);
      return res.status(response.status).json({ error: 'Gemini API error', detail: errorText });
    }

    const geminiJson = await response.json();
    const rawText = geminiJson?.candidates?.[0]?.content?.parts?.[0]?.text;

    if (!rawText) {
      return res.status(500).json({ error: 'Geminiのレスポンスからテキストを抽出できませんでした' });
    }

    // コードブロック除去
    let cleaned = rawText.trim();
    if (cleaned.startsWith('```json')) cleaned = cleaned.slice(7);
    else if (cleaned.startsWith('```')) cleaned = cleaned.slice(3);
    if (cleaned.endsWith('```')) cleaned = cleaned.slice(0, -3);
    cleaned = cleaned.trim();

    let itemData;
    try {
      itemData = JSON.parse(cleaned);
    } catch (e) {
      console.error('JSON パース失敗:', cleaned);
      return res.status(500).json({ error: 'JSON parse error', raw: cleaned });
    }

    return res.status(200).json(itemData);

  } catch (err) {
    console.error('Proxy エラー:', err);
    return res.status(500).json({ error: 'Internal server error', detail: err.message });
  }
}
