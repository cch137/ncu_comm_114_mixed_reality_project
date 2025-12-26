import { Hono } from "hono";
import { google } from "@ai-sdk/google";
import {
  ObjectGenerationOptionsSchema,
  ObjectGenerationTask,
} from "../services/workflows/object-designer";

const objectDesigner = new Hono();

objectDesigner.get("/tasks/:id", async (c) => {
  const state = ObjectGenerationTask.getState(c.req.param("id"));
  if (state === null) return c.json({ error: "Task not found" }, 404);
  return c.json(state, 200);
});

objectDesigner.post("/tasks", async (c) => {
  const { model, ...formData } = await c.req.json();
  if (typeof model !== "string") {
    return c.json(
      { error: "Model parameter is required and must be a string" },
      400
    );
  }
  try {
    const createParams = ObjectGenerationOptionsSchema.parse({
      ...formData,
      languageModel: google(model),
    });
    try {
      ObjectGenerationTask.create(createParams).execute();
      return c.json({ success: true }, 200);
    } catch {
      return c.json({ error: "Failed to create task" }, 500);
    }
  } catch {
    return c.json({ error: "Invalid request parameters" }, 400);
  }
});

objectDesigner.post("/tasks/:id/cancel", async (c) => {
  const exists = ObjectGenerationTask.cancel(c.req.param("id"));
  if (!exists) return c.json({ error: "Task not found" }, 404);
  return c.json({ success: true }, 200);
});

objectDesigner.delete("/tasks/:id", async (c) => {
  const deleted = ObjectGenerationTask.delete(c.req.param("id"));
  if (!deleted) return c.json({ error: "Task not found" }, 404);
  return c.json({ success: true }, 200);
});

export default objectDesigner;
