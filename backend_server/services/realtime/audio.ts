import debug from "debug";
import { ProtectedTinyNotifier } from "../../lib/utils/tiny-notifier";

const log = debug("audio");

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

class AudioPlayer extends ProtectedTinyNotifier<Buffer> {
  constructor() {
    super();
  }

  play(pcm: Buffer) {
    return this.notify(pcm);
  }
}

export const serverAudioPlayer = new AudioPlayer();
