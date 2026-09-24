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

  // ---- 強化版システムプロンプト ----
  // scaleの上限明示 + 2Dスプライト特化imagePrompt指示
  const systemInstruction = [
    'あなたは理不尽でスケールが暴走した神様です。',
    'プレイヤーの願いに対し、斜め上の解釈で解決する巨大な物体を考えてください。',
    '出力は必ず次のJSONフォーマットのみとし、コードブロック(```)や前後の余計な文章は一切含めないでください。',
    'scaleは0.5〜4.0の範囲で指定し、999999などの極端な値は絶対に禁止です。',
    'imagePromptは英語で書き、必ず以下のキーワードを先頭に含めてください:',
    '"white background, 2d game sprite, single object, flat cartoon style, clean bold outlines, no shadow, no gradient, centered"',
    'その後にオブジェクトの具体的な外見を英語で描写してください。',
    '',
    '出力フォーマット(このまま出力、余計な文字は一切不要):',
    '{"itemName":"英語名","imagePrompt":"white background, 2d game sprite, single object, flat cartoon style, clean bold outlines, no shadow, no gradient, centered, [具体的描写]","scale":2.0,"mass":30.0,"godMessage":"神様のセリフ（日本語可）"}'
  ].join('\n');

  const geminiUrl =
    `https://generativelanguage.googleapis.com/v1/models/gemini-3.6-flash:generateContent?key=${apiKey}`;

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

    // scaleを安全な範囲に制限（サーバー側でも二重チェック）
    itemData.scale = Math.min(Math.max(itemData.scale || 1.0, 0.5), 4.0);
    itemData.mass  = Math.min(Math.max(itemData.mass  || 1.0, 0.1), 100.0);

    return res.status(200).json(itemData);

  } catch (err) {
    console.error('Proxy エラー:', err);
    return res.status(500).json({ error: 'Internal server error', detail: err.message });
  }
};
