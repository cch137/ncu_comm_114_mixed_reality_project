import { Hono } from "hono";
import { WSContext } from "hono/ws";
import debug from "debug";

import { upgradeWebSocket } from "../server";
import { subscribeAudio } from "../services/realtime/audio";

const log = debug("monitor");

const monitor = new Hono();

const clients = new Set<WSContext<WebSocket>>();

subscribeAudio((buffer) => {
  for (const client of clients) {
    client.send(new Uint8Array(buffer));
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
        log(`[${id}] error`);
      },
      onMessage(_evt, _ws) {
        log(`[${id}] message`);
      },
    };
  })
);

export default monitor;
