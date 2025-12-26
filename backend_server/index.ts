import { config as dotenv } from "dotenv";
import { serve } from "@hono/node-server";
import { Hono } from "hono";
import { createNodeWebSocket } from "@hono/node-ws";
import { getConnInfo } from "@hono/node-server/conninfo";
import createDebug from "debug";

dotenv();

import dss from "./services/dss";
export * as threejsWorkflow from "./services/workflows/threejs";

const debug = createDebug("server");
const wsDebug = debug.extend("ws");

const app = new Hono();

const { injectWebSocket, upgradeWebSocket } = createNodeWebSocket({ app });

// simple logger: :method :url :status :res[content-length] - :response-time ms
app.use("*", async (c, next) => {
  const start = Date.now();
  await next();

  const ms = Date.now() - start;
  const info = getConnInfo(c);
  const ip = info.remote.address;
  const method = c.req.method;
  const url = c.req.url;
  const status = c.res.status;
  const contentLength = c.res.headers.get("content-length") ?? "0";

  debug(`${method} ${status} ${url} (${contentLength}b) (${ms}ms) ${ip}`);
});

app.route("", dss);

app.get("/", (c) => {
  return c.json({ status: "OK" });
});

app.get(
  "/mr-realtime",
  upgradeWebSocket((c) => {
    return {
      onOpen(event, ws) {
        wsDebug("Node.js WS connected");
      },
      onMessage(event, ws) {
        wsDebug(`Message: ${event.data}`);
        ws.send("Pong from Node!");
      },
      onClose(event, ws) {
        wsDebug("Disconnected");
      },
    };
  })
);

app.onError((err, c) => {
  debug("app error:", err);
  return c.text("Service Unavailable", 503);
});

process.on("uncaughtException", (error) => {
  debug("uncaughtException:", error);
});

process.on("unhandledRejection", (error) => {
  debug("unhandledRejection:", error);
});

const port =
  (process.env.PORT && Number.parseInt(process.env.PORT, 10)) || 3000;
const server = serve({ fetch: app.fetch, port }, (info) => {
  debug(`online @ http://localhost:${info.port}`);
});

injectWebSocket(server);

export default app;
