import { config as dotenv } from "dotenv";
import { serve } from "@hono/node-server";
import { Hono } from "hono";
import createDebug from "debug";

dotenv();

import dss from "./services/dss";

const bootDebug = createDebug("boot");

const app = new Hono();

app.route("", dss);

app.onError((err, c) => {
  bootDebug("app error:", err);
  return c.text("Service Unavailable", 503);
});

process.on("uncaughtException", (error) => {
  bootDebug("uncaughtException:", error);
});

process.on("unhandledRejection", (error) => {
  bootDebug("unhandledRejection:", error);
});

const port =
  (process.env.PORT && Number.parseInt(process.env.PORT, 10)) || 3000;

serve({ fetch: app.fetch, port }, (info) => {
  bootDebug(`online @ http://localhost:${info.port}`);
});
