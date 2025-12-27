import { config as dotenv } from "dotenv";
import createDebug from "debug";

dotenv();

import { app, upgradeWebSocket } from "./server";
import { realtimeHandler } from "./services/realtime/connection";
import dss from "./routers/dss";
import monitor from "./routers/monitor";
import objectDesigner from "./routers/object-designer";

const debug = createDebug("server");

app.route("", dss);
app.route("/monitor/", monitor);
app.route("/obj-dsgn/", objectDesigner);

app.get("/", (c) => {
  return c.json({ status: "OK" });
});

app.get("/mr-realtime", upgradeWebSocket(realtimeHandler));

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

export default app;
