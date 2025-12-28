import { Hono } from "hono";
import { WSContext } from "hono/ws";
import debug from "debug";

import { upgradeWebSocket } from "../server";
import { serverAudioPlayer } from "../services/realtime/audio";

const log = debug("monitor");

const monitor = new Hono();

const clients = new Set<WSContext<WebSocket>>();

serverAudioPlayer.subscribe((pcm) => {
  for (const client of clients) {
    try {
      client.send(new Uint8Array(pcm));
    } catch (err) {
      log("audio sending error:", err);
    }
  }
});

let monitorConnectionCounter = 0;

monitor.get(
  "/audio",
  upgradeWebSocket((_c) => {
    const id = `monitor${++monitorConnectionCounter}`;
    return {
      async onOpen(_evt, ws) {
        clients.add(ws);
        log(`[${id}] connected`);
      },
      onClose(_evt, ws) {
        clients.delete(ws);
        log(`[${id}] disconnected`);
      },
      onError(_evt, _ws) {
        log(`[${id}] an error occurred`);
      },
      async onMessage(event, _ws) {
        const buffer =
          typeof event.data === "string"
            ? Buffer.from(event.data, "binary")
            : event.data instanceof Blob
            ? Buffer.from(await event.data.bytes())
            : event.data instanceof ArrayBuffer
            ? Buffer.from(event.data)
            : null;
        if (buffer) serverAudioPlayer.play(buffer);
        log(`[${id}] sent a message`);
      },
    };
  })
);

export default monitor;
