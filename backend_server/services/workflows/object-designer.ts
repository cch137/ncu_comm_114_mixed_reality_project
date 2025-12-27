import path from "path";
import { readFile } from "fs/promises";
import { EventEmitter } from "events";
import createDebug from "debug";
import { Worker } from "worker_threads";
import { generateText, type LanguageModel } from "ai";
import { transpile, ModuleKind, ScriptTarget } from "typescript";
import Handlebars from "handlebars";
import z from "zod";

type ProviderOptions = Parameters<typeof generateText>[0]["providerOptions"];

export const ObjectPropsSchema = z.object({
  object_name: z.string().min(1),
  object_description: z.string().optional().default(""),
});

export type ObjectProps = z.infer<typeof ObjectPropsSchema>;

export const ObjectGenerationOptionsSchema = ObjectPropsSchema.extend({
  languageModel: z.custom<LanguageModel>((val) => {
    return typeof val === "object" && val !== null;
  }, "languageModel should be an object"),
  providerOptions: z
    .custom<ProviderOptions>((val) => {
      return typeof val === "object" && val !== null;
    }, "providerOptions should be an object")
    .optional(),
  vmTimeoutMs: z.number().optional(),
});

export type ObjectGenerationOptions = z.infer<
  typeof ObjectGenerationOptionsSchema
>;

export type ObjectGenerationState =
  | { status: Status.QUEUED; gltf: null; reason: null }
  | { status: Status.PROCESSING; gltf: null; reason: null }
  | { status: Status.COMPLETED; gltf: object; reason: null }
  | { status: Status.FAILED; gltf: null; reason: string }
  | { status: Status; gltf: object | null; reason: string | null };

export enum Status {
  QUEUED = "queued",
  PROCESSING = "processing",
  COMPLETED = "completed",
  FAILED = "failed",
}

const debug = createDebug("obj-dsgn");

async function loadInstructionsTemplate<T = {}>(name: string) {
  const filePath = path.resolve(
    process.cwd(),
    "instructions",
    `threejs-${name}.md`
  );
  const md = await readFile(filePath, "utf8");
  return Handlebars.compile<T>(md);
}

const instructions = (() => {
  const generationP = loadInstructionsTemplate<ObjectProps>("generation");

  return {
    generation: async (params: ObjectProps) => (await generationP)(params),
  };
})();

export function extractCodeFromMarkdown(markdown: string): string | null {
  const regex = /```(?:[^\n]*)\n([\s\S]*?)```\n?/;
  const match = markdown.match(regex);
  if (match) {
    return match[1];
  }
  const startTagIndex = markdown.indexOf("```");
  if (startTagIndex === -1) return null;
  const firstLineEndIndex = markdown.indexOf("\n", startTagIndex);
  if (firstLineEndIndex === -1) return "";
  const codeStartIndex = firstLineEndIndex + 1;
  const endTagIndex = markdown.indexOf("```", codeStartIndex);
  if (endTagIndex !== -1) {
    return markdown.substring(codeStartIndex, endTagIndex);
  } else {
    return markdown.substring(codeStartIndex);
  }
}

export async function generateThreeJsCodeForObject(options: {
  props: ObjectProps;
  model: LanguageModel;
  providerOptions?: ProviderOptions;
}) {
  const { text } = await generateText({
    model: options.model,
    prompt: await instructions.generation(options.props),
    providerOptions: options.providerOptions,
  });
  return extractCodeFromMarkdown(text) ?? text;
}

type WorkerLogs = {
  lines: { level: string; message: string; ts: number }[];
  dropped: number;
};

type GltfWorkerResult =
  | { success: true; gltf: object; logs: WorkerLogs }
  | { success: false; error: string; from: string; logs: WorkerLogs };

export function executeCodeAndExportGltf({
  code,
  timeoutMs = 10_000,
}: {
  code: string;
  timeoutMs?: number;
}) {
  return new Promise<object>((resolve, reject) => {
    const workerPath = path.resolve(__dirname, "./workers/threejs.js");

    const js = transpile(code, {
      module: ModuleKind.CommonJS,
      target: ScriptTarget.ES2022,
    });

    const worker = new Worker(workerPath, { workerData: { code: js } });

    const timer = setTimeout(() => {
      worker.terminate().catch(() => {});
      reject(new Error("Worker timeout"));
    }, timeoutMs);

    const done = (fn: () => void) => {
      clearTimeout(timer);
      worker.terminate().catch(() => {});
      fn();
    };

    worker.once("message", (msg: GltfWorkerResult) => {
      done(() => {
        if (msg.success) resolve(msg.gltf);
        else reject(new Error(`[${msg.from}] ${msg.error}`));
      });
    });

    worker.once("error", (err) => done(() => reject(err)));

    worker.once("exit", (code) => {
      if (code !== 0)
        done(() => reject(new Error(`Worker exited with code ${code}`)));
    });
  });
}

export class ObjectGenerationTask extends EventEmitter<{
  statusChange: [newStatus: Status, oldStatus: Status];
}> {
  private static readonly record = new Map<string, ObjectGenerationTask>();

  static create(options: ObjectGenerationOptions) {
    return new ObjectGenerationTask(options);
  }

  static get(id: string) {
    return this.record.get(id) ?? null;
  }

  static getState(id: string): ObjectGenerationState | null {
    return this.record.get(id)?.getState() ?? null;
  }

  static cancel(id: string) {
    const task = this.record.get(id);
    if (task) {
      task.cancel();
      return true;
    }
    return false;
  }

  static delete(id: string) {
    this.cancel(id);
    return this.record.delete(id);
  }

  static cleanupInactive(ttlMs: number) {
    const now = Date.now();
    for (const [id, task] of this.record.entries()) {
      if (
        (task.status === Status.COMPLETED || task.status === Status.FAILED) &&
        task.endAtMs !== null &&
        now - task.endAtMs >= ttlMs
      ) {
        this.record.delete(id);
      }
    }
  }

  readonly id: string;
  readonly objectProps: ObjectProps;
  readonly languageModel: LanguageModel;
  readonly providerOptions?: ProviderOptions;
  private cancelled = false;
  private taskPromise: Promise<void> | null = null;
  private _status: Status = Status.QUEUED;
  protected gltf: object | null = null;
  protected reason: string | null = null;
  private endAtMs: number | null = null;
  private readonly vmTimeoutMs?: number;

  private constructor({
    object_name,
    object_description,
    languageModel,
    providerOptions,
    vmTimeoutMs,
  }: ObjectGenerationOptions) {
    super();
    object_name = object_name.trim().replace(/\s+/g, "_");
    this.objectProps = { object_name, object_description };
    this.languageModel = languageModel;
    this.providerOptions = providerOptions;
    this.id = crypto.randomUUID();
    this.vmTimeoutMs = vmTimeoutMs;
    ObjectGenerationTask.record.set(this.id, this);
    debug(
      `task[${this.id}] queued for object '${this.objectProps.object_name}'`
    );
  }

  private get status() {
    return this._status;
  }

  private set status(status: Status) {
    if (status === this._status) return;
    const oldStatus = this._status;
    this._status = status;
    this.emit("statusChange", status, oldStatus);
    debug(`task[${this.id}] status changed from '${oldStatus}' to '${status}'`);
  }

  getState(): ObjectGenerationState {
    return { status: this.status, gltf: this.gltf, reason: this.reason };
  }

  execute() {
    if (this.taskPromise) return this.taskPromise;

    this.status = Status.PROCESSING;
    this.taskPromise = new Promise<void>((resolve) => {
      if (this.cancelled) return resolve();

      generateThreeJsCodeForObject({
        props: this.objectProps,
        model: this.languageModel,
        providerOptions: this.providerOptions,
      })
        .then(async (code) => {
          if (this.cancelled) return;
          const gltf = await executeCodeAndExportGltf({
            code,
            timeoutMs: this.vmTimeoutMs,
          });
          this.gltf = gltf;
          this.reason = null;
          this.status = Status.COMPLETED;
        })
        .catch((err) => {
          if (this.cancelled) return;
          this.gltf = null;
          this.reason = (err as Error)?.message ?? String(err);
          this.status = Status.FAILED;
        })
        .finally(() => {
          resolve();
          if (this.endAtMs === null) this.endAtMs = Date.now();
        });
    });

    return this.taskPromise;
  }

  cancel() {
    if (this.status === Status.COMPLETED || this.status === Status.FAILED) {
      return;
    }
    this.cancelled = true;
    this.gltf = null;
    this.reason = "Task was cancelled";
    this.status = Status.FAILED;
    this.endAtMs = Date.now();
  }
}

// auto cleaning relay store
(() => {
  const CLEANUP_INTERVAL_MS = 15 * 60 * 1000; // 15 minute
  const INACTIVITY_TTL_MS = 5 * 60 * 1000; // 5 minutes

  setInterval(() => {
    ObjectGenerationTask.cleanupInactive(INACTIVITY_TTL_MS);
  }, CLEANUP_INTERVAL_MS);
})();

export default {};
