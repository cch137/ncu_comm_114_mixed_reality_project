import path from "path";
import { mkdir, writeFile } from "fs/promises";
import { EventEmitter } from "events";
import debug from "debug";
import { type LanguageModel } from "ai";
import z from "zod";
import { loadInstructionsTemplateSync } from "../instructions";
import { generateCode } from "../workflows/generate-code";
import { generateGlbFromCode } from "../workflows/generate-glb-from-code";
import {
  ObjectProps,
  ObjectPropsSchema,
  ProviderOptions,
} from "../workflows/schemas";

const OUTPUT_DIRNAME = "public/output/workflows/object-designer/";

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

export class ObjectGenerationTask extends EventEmitter<{
  statusChange: [newStatus: Status, oldStatus: Status];
}> {
  private static readonly record = new Map<string, ObjectGenerationTask>();
  private static readonly renderThreeJsGenerationPrompt =
    loadInstructionsTemplateSync<ObjectProps>("threejs-generation");

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

      const instructions = ObjectGenerationTask.renderThreeJsGenerationPrompt(
        this.objectProps
      );

      generateCode({
        prompt: instructions,
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
          const glb = await generateGlbFromCode({
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
