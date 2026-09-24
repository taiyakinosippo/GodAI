/**
 * GodAI — Gemini + Pixel Art Proxy (Vercel Serverless Function)
 *
 * エンドポイント1: POST /api/gemini-proxy  → Geminiへ願いを送りGodItemDataを返す
 * エンドポイント2: POST /api/image-proxy   → Hugging FaceでPixel Artを生成しBase64で返す
 *
 * APIキーは Vercel 環境変数にのみ保管。
 */
module.exports = async function handler(req, res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') return res.status(204).end();
  if (req.method !== 'POST') return res.status(405).json({ error: 'Method Not Allowed' });

  const apiKey = process.env.GEMINI_API_KEY;
  if (!apiKey) return res.status(500).json({ error: 'Server configuration error' });

  const { wish } = req.body;
  if (!wish || typeof wish !== 'string') return res.status(400).json({ error: '`wish` が必要です' });

  const systemInstruction = [
    'あなたは理不尽でスケールが暴走した神様です。',
    'プレイヤーの願いに対し、斜め上の解釈で解決する巨大な物体を考えてください。',
    '出力は必ず次のJSONフォーマットのみとし、コードブロック(```)や前後の余計な文章は一切含めないでください。',
    'scaleは0.5〜4.0の範囲で指定し、999999などの極端な値は絶対に禁止です。',
    'imagePromptはドット絵ゲームスプライト向けの英語プロンプトにしてください。',
    '必ず以下のキーワードを先頭に含めること:',
    '"pixel art, 16-bit sprite, white background, single object, clean edges, game asset, retro style"',
    '{"itemName":"英語名","imagePrompt":"pixel art, 16-bit sprite, white background, single object, clean edges, game asset, retro style, [具体的描写]","scale":2.0,"mass":30.0,"godMessage":"神様のセリフ（日本語可）"}'
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
      return res.status(response.status).json({ error: 'Gemini API error', detail: errorText });
    }

    const geminiJson = await response.json();
    const rawText = geminiJson?.candidates?.[0]?.content?.parts?.[0]?.text;
    if (!rawText) return res.status(500).json({ error: 'Geminiからテキスト抽出失敗' });

    let cleaned = rawText.trim();
    if (cleaned.startsWith('```json')) cleaned = cleaned.slice(7);
    else if (cleaned.startsWith('```')) cleaned = cleaned.slice(3);
    if (cleaned.endsWith('```')) cleaned = cleaned.slice(0, -3);
    cleaned = cleaned.trim();

    let itemData;
    try { itemData = JSON.parse(cleaned); }
    catch (e) { return res.status(500).json({ error: 'JSON parse error', raw: cleaned }); }

    // scaleとmassをサーバー側でも安全な範囲にクランプ
    itemData.scale = Math.min(Math.max(itemData.scale || 1.0, 0.5), 4.0);
    itemData.mass  = Math.min(Math.max(itemData.mass  || 1.0, 0.1), 100.0);

    return res.status(200).json(itemData);

  } catch (err) {
    return res.status(500).json({ error: 'Internal server error', detail: err.message });
  }
};
