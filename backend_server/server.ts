import { Hono } from "hono";
import { createNodeWebSocket } from "@hono/node-ws";
import { serve } from "@hono/node-server";
import { getConnInfo } from "@hono/node-server/conninfo";
import { serveStatic } from "@hono/node-server/serve-static";
import createDebug from "debug";

const debug = createDebug("server");

export const app = new Hono();

const { injectWebSocket, upgradeWebSocket } = createNodeWebSocket({
  app,
});

export { upgradeWebSocket };

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

app.use(
  "/*",
  serveStatic({
    root: "./public/",
    onFound: (_path, c) => {
      c.header(
        "Cache-Control",
        "no-store, no-cache, must-revalidate, max-age=0"
      );
      c.header("Pragma", "no-cache");
      c.header("Expires", "0");
    },
  })
);

const port =
  (process.env.PORT && Number.parseInt(process.env.PORT, 10)) || 3000;

export const server = serve({ fetch: app.fetch, port }, (info) => {
  debug(`online @ http://localhost:${info.port}`);
});

injectWebSocket(server);
