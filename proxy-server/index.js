const functions = require('@google-cloud/functions-framework');

/**
 * GodAI Gemini Proxy
 * Unity から願いテキストを受け取り、Gemini API へ転送して結果を返す。
 * APIキーはこのコード内に存在せず、環境変数 GEMINI_API_KEY から読み込む。
 */
functions.http('geminiProxy', async (req, res) => {
  // CORS ヘッダ（Unity WebGL / 開発時対応）
  res.set('Access-Control-Allow-Origin', '*');
  res.set('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.set('Access-Control-Allow-Headers', 'Content-Type');

  // プリフライトリクエスト対応
  if (req.method === 'OPTIONS') {
    res.status(204).send('');
    return;
  }

  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method Not Allowed' });
    return;
  }

  // 環境変数からAPIキーを取得（コードには書かない）
  const apiKey = process.env.GEMINI_API_KEY;
  if (!apiKey) {
    console.error('GEMINI_API_KEY が環境変数に設定されていません');
    res.status(500).json({ error: 'Server configuration error' });
    return;
  }

  // Unityから受け取る願いテキスト
  const { wish } = req.body;
  if (!wish || typeof wish !== 'string') {
    res.status(400).json({ error: '`wish` フィールドが必要です' });
    return;
  }

  const systemInstruction =
    'あなたは理不尽でスケールが暴走した神様です。' +
    'プレイヤーの願いに対し、斜め上の解釈で解決する巨大な物体を考えてください。\n' +
    '出力は必ず次のJSONフォーマットのみとし、コードブロック(```)や前後の余計な文章は一切含めないでください。\n' +
    '{"itemName":"英語名","imagePrompt":"white background, 2d game sprite, single object, 英語プロンプト",' +
    '"scale":2.0,"mass":30.0,"godMessage":"神様のセリフ（日本語可）"}';

  const geminiUrl =
    `https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=${apiKey}`;

  const payload = {
    system_instruction: { parts: [{ text: systemInstruction }] },
    contents: [{ parts: [{ text: `願い: ${wish}` }] }]
  };

  try {
    const response = await fetch(geminiUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(55000) // 55秒でタイムアウト
    });

    if (!response.ok) {
      const errorText = await response.text();
      console.error('Gemini API エラー:', response.status, errorText);
      res.status(response.status).json({ error: 'Gemini API error', detail: errorText });
      return;
    }

    // Geminiのレスポンスからテキスト部分を抽出
    const geminiJson = await response.json();
    const rawText = extractText(geminiJson);

    if (!rawText) {
      res.status(500).json({ error: 'Geminiのレスポンスからテキストを抽出できませんでした' });
      return;
    }

    // コードブロックの除去
    const cleanedText = stripCodeBlock(rawText);

    // JSONとしてパース・検証してからUnityへ返す
    let itemData;
    try {
      itemData = JSON.parse(cleanedText);
    } catch (e) {
      console.error('JSON パース失敗:', cleanedText);
      res.status(500).json({ error: 'JSON parse error', raw: cleanedText });
      return;
    }

    res.status(200).json(itemData);

  } catch (err) {
    console.error('Proxy エラー:', err);
    res.status(500).json({ error: 'Internal server error', detail: err.message });
  }
});

/** Gemini APIレスポンスから candidates[0].content.parts[0].text を取り出す */
function extractText(geminiJson) {
  try {
    return geminiJson.candidates[0].content.parts[0].text;
  } catch (_) {
    return null;
  }
}

/** ```json ... ``` / ``` ... ``` を除去する */
function stripCodeBlock(text) {
  text = text.trim();
  if (text.startsWith('```json')) text = text.slice(7);
  else if (text.startsWith('```')) text = text.slice(3);
  if (text.endsWith('```')) text = text.slice(0, -3);
  return text.trim();
}
