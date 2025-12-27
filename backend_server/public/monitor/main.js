// main.js
(async () => {
  const startButton = document.getElementById("start-btn");
  if (!startButton) throw new Error("start button not found");

  startButton.addEventListener("click", start, { once: true });

  const url = `${location.origin.replace(/^http/, "ws")}/monitor/audio`;

  function updateStatus(text) {
    startButton.textContent = text;
  }

  function start() {
    startButton.disabled = true;
    updateStatus("Connecting...");
    connect();
  }

  function connect() {
    const ws = new WebSocket(url);
    ws.binaryType = "arraybuffer";

    // PCM spec (must match server)
    const sampleRate = 16000;
    const channels = 1;

    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    const ctx = new AudioCtx({ sampleRate });

    // Some browsers require a user gesture to start audio.
    const resume = async () => {
      if (ctx.state !== "running") await ctx.resume();
    };

    window.addEventListener("pointerdown", resume, { once: true });
    window.addEventListener("keydown", resume, { once: true });

    let isClosed = false;
    let nextStartTime = 0;

    function pcm16leToFloat32(int16) {
      const out = new Float32Array(int16.length);
      for (let i = 0; i < int16.length; i++) out[i] = int16[i] / 0x8000;
      return out;
    }

    ws.addEventListener("open", () => {
      updateStatus("Connected.");
    });

    ws.addEventListener("message", async (ev) => {
      console.log("ws received message");

      await resume(); // in case first audio arrives after gesture

      const ab = ev.data; // ArrayBuffer
      /* PCM16LE (16 bit depth, signed little-endian PCM) */
      const int16 = new Int16Array(ab);
      const float32 = pcm16leToFloat32(int16);

      const frameCount = float32.length / channels;
      if (!Number.isInteger(frameCount)) return;

      const buffer = ctx.createBuffer(channels, frameCount, sampleRate);
      // mono
      buffer.getChannelData(0).set(float32);

      const src = ctx.createBufferSource();
      src.buffer = buffer;
      src.connect(ctx.destination);

      // schedule sequentially to avoid gaps
      const now = ctx.currentTime;
      if (nextStartTime < now + 0.02) nextStartTime = now + 0.02;
      src.start(nextStartTime);
      nextStartTime += buffer.duration;
    });

    ws.addEventListener("error", () => {
      isClosed = true;
      updateStatus("Error.");
    });

    ws.addEventListener("close", () => {
      isClosed = true;
      updateStatus("Disconnected.");
      ctx.close();
      connect();
    });
  }
})();
