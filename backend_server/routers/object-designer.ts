import { Hono } from "hono";
import { openai } from "@ai-sdk/openai";
import { google } from "@ai-sdk/google";
import {
  ObjectGenerationOptionsSchema,
  ObjectGenerationTask,
} from "../services/workflows/object-designer";
import { RealtimeRoom } from "../services/realtime/connection";

const objectDesigner = new Hono();

objectDesigner.get("/tasks/:id", async (c) => {
  const state = ObjectGenerationTask.getState(c.req.param("id"));
  if (state === null)
    return c.json({ success: false, error: "Task not found" }, 404);
  return c.json({ success: true, data: state }, 200);
});

objectDesigner.get("/tasks/:id/object", async (c) => {
  const state = ObjectGenerationTask.getState(c.req.param("id"));
  if (state === null) return c.body(null, 404);
  if (!state.object) return c.body(null, 404);
  return c.body(new Uint8Array(state.object), 200, {
    "Content-Type": "model/gltf-binary",
  });
});

objectDesigner.post("/tasks", async (c) => {
  const { model, ...formData } = await c.req.json();
  if (typeof model !== "string") {
    return c.json(
      {
        success: false,
        error: "Model parameter is required and must be a string",
      },
      400
    );
  }
  try {
    const createParams = ObjectGenerationOptionsSchema.parse({
      ...formData,
      languageModel: model.startsWith("gemini-")
        ? google(model)
        : openai(model),
    });
    try {
      const task = ObjectGenerationTask.create(createParams);
      task.execute();
      return c.json({ success: true, data: { id: task.id } }, 200);
    } catch {
      return c.json({ success: false, error: "Failed to create task" }, 500);
    }
  } catch {
    return c.json({ success: false, error: "Invalid request parameters" }, 400);
  }
});

objectDesigner.post("/tasks/:id/cancel", async (c) => {
  const exists = ObjectGenerationTask.cancel(c.req.param("id"));
  if (!exists) return c.json({ success: false, error: "Task not found" }, 404);
  return c.json({ success: true }, 200);
});

objectDesigner.delete("/tasks/:id", async (c) => {
  const deleted = ObjectGenerationTask.delete(c.req.param("id"));
  if (!deleted) return c.json({ success: false, error: "Task not found" }, 404);
  return c.json({ success: true }, 200);
});

objectDesigner.post("/_debug_add_prog_obj_rooms", async (c) => {
  try {
    RealtimeRoom._debug_add_prog_obj_rooms(await c.req.json());
    return c.json({ success: true }, 200);
  } catch (error) {
    return c.json({ success: false, error: String(error) }, 500);
  }
});

export default objectDesigner;
