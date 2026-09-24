/**
 * GodAI — Pixel Art Image Proxy (Vercel Serverless Function)
 *
 * Unity から { "prompt": "pixel art prompt" } を受け取り、
 * Hugging Face の Inference API でドット絵を生成してBase64 PNGで返す。
 * アカウントなしの無料枠でも動作する。
 */
module.exports = async function handler(req, res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') return res.status(204).end();
  if (req.method !== 'POST') return res.status(405).json({ error: 'Method Not Allowed' });

  const { prompt } = req.body;
  if (!prompt || typeof prompt !== 'string') {
    return res.status(400).json({ error: '`prompt` が必要です' });
  }

  // Hugging Face Token（任意。設定すると優先度が上がる）
  const hfToken = process.env.HF_TOKEN || '';

  // pixel-art-xl モデル（無料・透過に強い）
  const HF_MODEL = 'nerijs/pixel-art-xl';
  const url = `https://api-inference.huggingface.co/models/${HF_MODEL}`;

  const headers = { 'Content-Type': 'application/json' };
  if (hfToken) headers['Authorization'] = `Bearer ${hfToken}`;

  const body = JSON.stringify({
    inputs: prompt,
    parameters: {
      width: 512,
      height: 512,
      num_inference_steps: 20,
      guidance_scale: 7.5
    },
    options: {
      wait_for_model: true  // モデルのウォームアップを待つ
    }
  });

  try {
    console.log('[image-proxy] HF リクエスト開始:', prompt.slice(0, 80));

    const response = await fetch(url, {
      method: 'POST',
      headers,
      body,
      signal: AbortSignal.timeout(110000)  // 110秒タイムアウト
    });

    if (!response.ok) {
      const errText = await response.text();
      console.error('[image-proxy] HF エラー:', response.status, errText);

      // モデルロード中（503）の場合はPollinationsにフォールバック
      if (response.status === 503) {
        console.log('[image-proxy] モデルロード中 → Pollinationsにフォールバック');
        return await pollinationsFallback(prompt, res);
      }

      return res.status(response.status).json({ error: 'HF API error', detail: errText });
    }

    // バイナリ画像データをBase64に変換
    const arrayBuffer = await response.arrayBuffer();
    const base64 = Buffer.from(arrayBuffer).toString('base64');

    console.log('[image-proxy] 画像生成成功');
    return res.status(200).json({
      imageBase64: base64,
      mimeType: 'image/png',
      source: 'huggingface'
    });

  } catch (err) {
    console.error('[image-proxy] エラー:', err.message);

    // タイムアウト時もPollinationsにフォールバック
    if (err.name === 'TimeoutError' || err.message.includes('timeout')) {
      console.log('[image-proxy] タイムアウト → Pollinationsにフォールバック');
      return await pollinationsFallback(prompt, res);
    }

    return res.status(500).json({ error: 'Internal server error', detail: err.message });
  }
};

/**
 * HFが使えない場合のフォールバック: Pollinations.aiで画像URLを返す
 */
async function pollinationsFallback(prompt, res) {
  const encodedPrompt = encodeURIComponent(prompt);
  const imageUrl = `https://image.pollinations.ai/prompt/${encodedPrompt}?width=512&height=512&nologo=true`;

  return res.status(200).json({
    imageUrl,
    source: 'pollinations'
  });
}
