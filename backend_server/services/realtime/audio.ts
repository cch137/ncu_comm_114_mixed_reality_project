import { ProtectedTinyNotifier } from "../../lib/utils/tiny-notifier";

export const SERVER_AUDIO_SAMPLE_RATE = 16_000;

export function* chunkPcmBufferRandomDurations(
  buffer: Buffer,
  spec = { sampleRate: 16000, bitDepth: 16, channels: 1 }
) {
  const bytesPerSample = spec.bitDepth / 8;
  const frameSize = bytesPerSample * spec.channels;

  let offset = 0;

  while (offset < buffer.length) {
    const randomDuration = Math.random() * 0.4 + 0.1; // 0.1–0.5s
    const targetByteLength =
      Math.floor(spec.sampleRate * randomDuration) * frameSize;
    const remainingBytes = buffer.length - offset;
    const actualByteLength = Math.min(targetByteLength, remainingBytes);
    const chunk = buffer.subarray(offset, offset + actualByteLength);
    yield chunk;
    offset += actualByteLength;
  }
}

export function resamplePcmAudioBuffer(
  buffer: Buffer,
  fromSampleRate: number,
  toSampleRate: number
): Buffer {
  const input = new Int16Array(
    buffer.buffer,
    buffer.byteOffset,
    buffer.length / 2
  );
  const outputLength = Math.floor(
    (input.length * toSampleRate) / fromSampleRate
  );
  const output = new Int16Array(outputLength);

  const ratio = input.length / outputLength;

  for (let i = 0; i < outputLength; i++) {
    const pos = i * ratio;
    const idx = Math.floor(pos);
    const frac = pos - idx;

    const sample1 = input[idx];
    const sample2 = input[Math.min(idx + 1, input.length - 1)];

    output[i] = Math.round(sample1 + (sample2 - sample1) * frac);
  }

  return Buffer.from(output.buffer);
}

class AudioNotifier extends ProtectedTinyNotifier<Buffer> {
  constructor() {
    super();
  }

  play(pcm: Buffer) {
    return this.notify(pcm);
  }
}

export const serverMic = new AudioNotifier();
export const serverSpeaker = new AudioNotifier();
