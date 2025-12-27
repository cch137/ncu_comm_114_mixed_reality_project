import debug from "debug";

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

const subscribers = new Set<(buffer: Buffer) => void>();

export function subscribeAudio(cb: (buffer: Buffer) => void) {
  subscribers.add(cb);
}

export function unsubscribeAudio(cb: (buffer: Buffer) => void) {
  subscribers.delete(cb);
}

export function playAudio(fileBuffer: Buffer) {
  for (const subscriber of subscribers) {
    try {
      subscriber(fileBuffer);
    } catch (error) {
      log("audio subscriber error:", error);
    }
  }
}
