import path from "path";
import { mkdir, writeFile } from "fs/promises";
import { EventEmitter } from "events";
import debug from "debug";
import { Worker } from "worker_threads";
import { generateText, type LanguageModel } from "ai";
import { transpile, ModuleKind, ScriptTarget } from "typescript";
import z from "zod";
import { loadInstructionsTemplate } from "../instructions";

const OUTPUT_DIRNAME = "public/output/workflows/object-designer/";

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
  | { status: Status.QUEUED; object: null; reason: null }
  | { status: Status.PROCESSING; object: null; reason: null }
  | { status: Status.COMPLETED; object: Uint8Array<ArrayBuffer>; reason: null }
  | { status: Status.FAILED; object: null; reason: string }
  | {
      status: Status;
      object: Uint8Array<ArrayBuffer> | null;
      reason: string | null;
    };

export enum Status {
  QUEUED = "queued",
  PROCESSING = "processing",
  COMPLETED = "completed",
  FAILED = "failed",
}

const log = debug("obj-dsgn");

const instructions = (() => {
  const generationP =
    loadInstructionsTemplate<ObjectProps>("threejs-generation");

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

const WorkerLogsSchema = z.object({
  lines: z.array(
    z.object({
      level: z.string(),
      message: z.string(),
      ts: z.number(),
    })
  ),
  dropped: z.number(),
});

const GltfWorkerResultSchema = z.union([
  z.object({
    success: z.literal(true),
    object: z.instanceof(ArrayBuffer),
    logs: WorkerLogsSchema,
  }),
  z.object({
    success: z.literal(false),
    error: z.string(),
    from: z.string(),
    logs: WorkerLogsSchema,
  }),
]);

type GltfWorkerResult = z.infer<typeof GltfWorkerResultSchema>;

export function executeCodeAndExportGlb({
  code,
  timeoutMs = 10_000,
}: {
  code: string;
  timeoutMs?: number;
}) {
  return new Promise<Uint8Array<ArrayBuffer>>((resolve, reject) => {
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

    worker.once("message", (msg: unknown) => {
      done(async () => {
        const parsed = GltfWorkerResultSchema.safeParse(msg);
        if (!parsed.success) {
          return reject(new Error("Invalid worker message"));
        }
        const result = parsed.data;
        if (result.success) {
          return resolve(new Uint8Array(result.object));
        } else {
          return reject(new Error(`[${result.from}] ${result.error}`));
        }
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
  protected object: Uint8Array<ArrayBuffer> | null = null;
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
    log(`task[${this.id}] queued for object '${this.objectProps.object_name}'`);
  }

  private get status() {
    return this._status;
  }

  private set status(status: Status) {
    if (status === this._status) return;
    const oldStatus = this._status;
    this._status = status;
    this.emit("statusChange", status, oldStatus);
    log(`task[${this.id}] status changed from '${oldStatus}' to '${status}'`);
  }

  getState(): ObjectGenerationState {
    return {
      status: this.status,
      object: this.object,
      reason: this.reason,
    };
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
          const codeFilepath = path.join(OUTPUT_DIRNAME, this.id, "code.txt");
          const glbFilepath = path.join(
            OUTPUT_DIRNAME,
            this.id,
            `${this.objectProps.object_name}.glb`
          );

          mkdir(path.dirname(codeFilepath), { recursive: true })
            .then(async () => {
              await writeFile(codeFilepath, code, "utf8");
              log(`task[${this.id}] saved code`);
            })
            .catch((err) => log(`task[${this.id}] failed to save code`, err));

          if (this.cancelled) return;
          const glb = await executeCodeAndExportGlb({
            code,
            timeoutMs: this.vmTimeoutMs,
          });

          mkdir(path.dirname(glbFilepath), { recursive: true })
            .then(async () => {
              await writeFile(glbFilepath, glb, "binary");
              log(`task[${this.id}] saved glb`);
            })
            .catch((err) => log(`task[${this.id}] failed to save glb`, err));

          this.object = glb;
          this.reason = null;
          this.status = Status.COMPLETED;
        })
        .catch((err) => {
          if (this.cancelled) return;
          this.object = null;
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
    this.object = null;
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
