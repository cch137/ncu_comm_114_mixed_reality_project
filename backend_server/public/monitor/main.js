(async () => {
  const startButton = document.getElementById("start-btn");
  if (!startButton) throw new Error("start button not found");

  startButton.addEventListener("click", start, { once: true });

  const url = `${location.origin.replace(/^http/, "ws")}/monitor/audio`;

  async function start() {
    startButton.disabled = true;
    await connect();
  }

  async function connect(isReconnect = false) {
    // PCM spec (must match server)
    const sampleRate = 16000;
    const channels = 1;

    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    const ctx = new AudioCtx({ sampleRate });

    let isClosed = false;
    let nextStartTime = 0;
    let heartbeatInterval = null;

    const updateStatus = (text) => {
      if (isClosed) return;
      startButton.textContent = text;
    };

    // Some browsers require a user gesture to start audio.
    const resume = async () => {
      if (ctx.state !== "running") await ctx.resume();
    };

    const reconnect = async () => {
      if (isClosed) return;
      isClosed = true;
      clearInterval(heartbeatInterval);
      ctx.close();
      window.removeEventListener("pointerdown", resume);
      window.removeEventListener("keydown", resume);
      await new Promise((r) => setTimeout(r, 1_000));
      await connect(true);
    };

    const heartbeat = () => {
      if (isClosed) return clearInterval(heartbeatInterval);
      if (ws.readyState === WebSocket.OPEN) ws.send(new Uint8Array());
    };

    const setHeartbeat = () => {
      clearInterval(heartbeatInterval);
      heartbeatInterval = setInterval(heartbeat, 20_000);
    };

    window.addEventListener("pointerdown", resume, { once: true });
    window.addEventListener("keydown", resume, { once: true });

    const ws = new WebSocket(url);
    updateStatus(isReconnect ? "Reconnecting..." : "Connecting...");
    ws.binaryType = "arraybuffer";

    function pcm16leToFloat32(int16) {
      const out = new Float32Array(int16.length);
      for (let i = 0; i < int16.length; i++) out[i] = int16[i] / 0x8000;
      return out;
    }

    ws.addEventListener("open", () => {
      updateStatus("Connected");
      setHeartbeat();
    });

    ws.addEventListener("message", async (ev) => {
      setHeartbeat();

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

    ws.addEventListener("error", async () => {
      updateStatus("Error");
      if (
        ws.readyState !== WebSocket.CLOSING &&
        ws.readyState !== WebSocket.CLOSED
      ) {
        ws.close();
      }
    });

    ws.addEventListener("close", async () => {
      updateStatus("Disconnected");
      await reconnect();
    });
  }
})();
