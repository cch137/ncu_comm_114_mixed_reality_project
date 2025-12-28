import {
  GoogleGenAI,
  LiveServerMessage,
  Modality,
  Session,
} from "@google/genai";
import debug from "debug";
import {
  serverMic,
  serverSpeaker,
  SERVER_AUDIO_SAMPLE_RATE,
  resamplePcmAudioBuffer,
} from "./audio";
import z from "zod";

const log = debug("assistant");

const DEFAULT_MODEL = "gemini-2.5-flash-native-audio-preview-12-2025";
const INPUT_SAMPLE_RATE = 16_000;
const OUTPUT_SAMPLE_RATE = 24_000;
const AUDIO_SEND_INTERVAL_MS = 250;

const gai = new GoogleGenAI({
  apiKey: process.env.GOOGLE_GENERATIVE_AI_API_KEY,
});

export class RealtimeAssistant {
  private session: Session | null = null;
  private isConnected = false;

  private pendingAudioChunks: Buffer[] = [];
  private audioFlushTimer: NodeJS.Timeout | null = null;

  constructor() {}

  async connect(): Promise<void> {
    if (this.isConnected) {
      log("already connected");
      return;
    }

    try {
      this.session = await gai.live.connect({
        model: DEFAULT_MODEL,
        config: {
          responseModalities: [Modality.AUDIO],
          // thinkingConfig: {
          //   thinkingLevel: ThinkingLevel.HIGH,
          // },
          tools: [
            {
              functionDeclarations: [
                {
                  name: "turn_on_the_light",
                  parametersJsonSchema: z.toJSONSchema(
                    z.object({ confirm: z.boolean() })
                  ),
                },
                {
                  name: "turn_off_the_light",
                  parametersJsonSchema: z.toJSONSchema(
                    z.object({ confirm: z.boolean() })
                  ),
                },
              ],
            },
          ],
          systemInstruction: "你的名字是茶壺小姐。一個有趣的 AI 助手。",
        },
        callbacks: {
          onopen: () => this.handleOpen(),
          onmessage: (message) => this.handleMessage(message),
          onerror: (error) => this.handleError(error),
          onclose: (event) => this.handleClose(event),
        },
      });

      this.isConnected = true;
      this.startAudioFlushLoop();
    } catch (error) {
      log("connection failed:", error);
      throw error;
    }
  }

  private startAudioFlushLoop(): void {
    if (this.audioFlushTimer) return;

    this.audioFlushTimer = setInterval(() => {
      if (!this.isConnected || !this.session) return;
      if (this.pendingAudioChunks.length === 0) return;

      const pcm = Buffer.concat(this.pendingAudioChunks);
      this.pendingAudioChunks = [];

      try {
        const base64Audio = pcm.toString("base64");

        this.session.sendRealtimeInput({
          audio: {
            data: base64Audio,
            mimeType: `audio/pcm;rate=${INPUT_SAMPLE_RATE}`,
          },
        });

        log("sent audio chunk: %d bytes", pcm.length);
      } catch (error) {
        log("failed to send audio:", error);
      }
    }, AUDIO_SEND_INTERVAL_MS);
  }

  private stopAudioFlushLoop(): void {
    if (this.audioFlushTimer) {
      clearInterval(this.audioFlushTimer);
      this.audioFlushTimer = null;
    }
    this.pendingAudioChunks = [];
  }

  private handleOpen(): void {
    log("connected to Gemini Live API");
  }

  private handleMessage(message: LiveServerMessage): void {
    const { serverContent } = message;

    if (!serverContent) return;

    if (serverContent.interrupted) {
      log("response interrupted");
      return;
    }

    if (serverContent.modelTurn?.parts) {
      const parts = serverContent.modelTurn.parts;

      for (const part of parts) {
        if (part.inlineData?.data) {
          const audioBuffer = Buffer.from(part.inlineData.data, "base64");
          serverSpeaker.play(
            resamplePcmAudioBuffer(
              audioBuffer,
              OUTPUT_SAMPLE_RATE,
              SERVER_AUDIO_SAMPLE_RATE
            )
          );

          log("received audio chunk: %d bytes", audioBuffer.length);
        }

        if (part.functionCall) {
          log("received function call:", part.functionCall);
        } else if (part.functionResponse) {
          log("received function response:", part.functionResponse);
        }

        if (part.text) {
          log("received transcript: %s", part.text);
        }
      }
    }

    if (serverContent.turnComplete) {
      log("response complete");
    }
  }

  private handleError(error: ErrorEvent): void {
    log("API error:", error);
  }

  private handleClose(event: CloseEvent): void {
    log("connection closed: %s", event.reason);
    this.isConnected = false;
    this.stopAudioFlushLoop();
  }

  addAudioBuffer(pcm: Buffer): void {
    if (!this.isConnected || !this.session) {
      log("not connected");
      return;
    }

    // Queue audio; it will be sent every 250ms by the flush loop
    this.pendingAudioChunks.push(pcm);
  }

  sendText(text: string): void {
    if (!this.isConnected || !this.session) {
      log("not connected");
      return;
    }

    try {
      this.session.sendRealtimeInput({
        text: text,
      });
      log("sent text:", text);
    } catch (error) {
      log("failed to send text:", error);
    }
  }

  async disconnect(): Promise<void> {
    if (this.session) {
      try {
        this.session.close();
        log("disconnected");
      } catch (error) {
        log("disconnect error:", error);
      }
      this.session = null;
      this.isConnected = false;
      this.stopAudioFlushLoop();
    }
  }

  get connected(): boolean {
    return this.isConnected;
  }
}

(async () => {
  const assistant = new RealtimeAssistant();

  await assistant.connect();

  serverMic.subscribe((pcm) => {
    assistant.addAudioBuffer(pcm);
  });
})();
