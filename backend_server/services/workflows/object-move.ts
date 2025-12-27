import { EventEmitter } from "events";
import createDebug from "debug";
import { z } from "zod";
import { RealtimeClient, type Pose } from "../realtime/connection";

const debug = createDebug("mv-fn");

// -------------------- Schemas --------------------
export const ObjectPropsSchema = z.object({
  object_name: z.string().min(1),
  object_description: z.string().optional().default(""),
});

export type ObjectProps = z.infer<typeof ObjectPropsSchema>;

// -------------------- Schemas --------------------
export const ObjectPropsSchema = z.object({
  object_name: z.string().min(1),
  object_description: z.string().optional().default(""),
});

export type ObjectProps = z.infer<typeof ObjectPropsSchema>;

export const PoseSchema = z.object({
  pos: z.tuple([z.number(), z.number(), z.number()]),
  rot: z.tuple([z.number(), z.number(), z.number(), z.number()]),
});

export const MoveCommandSchema = z.object({
  object_id: z.string().min(1),
  to: PoseSchema,
  durationMs: z.number().int().min(0).max(60_000).optional(),
});

export type MoveCommand = z.infer<typeof MoveCommandSchema>;

// -------------------- Task / State (對齊 object-designer.ts 風格) --------------------

export enum MoveStatus {
  QUEUED = "queued",
  PROCESSING = "processing",
  COMPLETED = "completed",
  FAILED = "failed",
}

export type MoveTaskState =
  | { status: MoveStatus.QUEUED; finalPose: null; reason: null }
  | { status: MoveStatus.PROCESSING; finalPose: null; reason: null }
  | { status: MoveStatus.COMPLETED; finalPose: Pose; reason: null }
  | { status: MoveStatus.FAILED; finalPose: null; reason: string };

export class MoveObjectTask extends EventEmitter<{
  statusChange: [newStatus: MoveStatus, oldStatus: MoveStatus];
}> {
  private static readonly record = new Map<string, MoveObjectTask>();

  static create(options: {
    client: RealtimeClient;
    command: MoveCommand;
    timeoutMs?: number;
  }) {
    return new MoveObjectTask(options);
  }

  static get(id: string) {
    return this.record.get(id) ?? null;
  }

  static getState(id: string): MoveTaskState | null {
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
        (task.status === MoveStatus.COMPLETED ||
          task.status === MoveStatus.FAILED) &&
        task.endAtMs !== null &&
        now - task.endAtMs >= ttlMs
      ) {
        this.record.delete(id);
      }
    }
  }

  readonly id: string;
  private readonly client: RealtimeClient;
  private readonly command: MoveCommand;
  private readonly timeoutMs: number;

  private cancelled = false;
  private taskPromise: Promise<void> | null = null;

  private _status: MoveStatus = MoveStatus.QUEUED;
  private finalPose: Pose | null = null;
  private reason: string | null = null;

  private endAtMs: number | null = null;

  private constructor(options: {
    client: RealtimeClient;
    command: MoveCommand;
    timeoutMs?: number;
  }) {
    super();

    const command = MoveCommandSchema.parse(options.command);

    this.client = options.client;
    this.command = command;
    this.timeoutMs = options.timeoutMs ?? 10_000;

    this.id = crypto.randomUUID();
    MoveObjectTask.record.set(this.id, this);

    debug(
      `task[${this.id}] queued move for object '${this.command.object_id}'`
    );
  }

  private get status() {
    return this._status;
  }

  private set status(status: MoveStatus) {
    if (status === this._status) return;
    const oldStatus = this._status;
    this._status = status;
    this.emit("statusChange", status, oldStatus);
    debug(`task[${this.id}] status changed from '${oldStatus}' to '${status}'`);
  }

  getState(): MoveTaskState {
    if (this.status === MoveStatus.COMPLETED) {
      return {
        status: this.status,
        finalPose: this.finalPose as Pose,
        reason: null,
      };
    }
    if (this.status === MoveStatus.FAILED) {
      return {
        status: this.status,
        finalPose: null,
        reason: this.reason ?? "unknown error",
      };
    }
    return { status: this.status, finalPose: null, reason: null };
  }

  execute() {
    if (this.taskPromise) return this.taskPromise;

    this.status = MoveStatus.PROCESSING;
    this.taskPromise = new Promise<void>((resolve) => {
      if (this.cancelled) return resolve();

      const requestId = crypto.randomUUID();

      this.client.sendMoveObject({
        requestId,
        objectId: this.command.object_id,
        to: this.command.to,
        durationMs: this.command.durationMs,
      });

      this.client
        .waitMoveAck(requestId, this.timeoutMs)
        .then((ack) => {
          if (this.cancelled) return;

          if (ack.ok) {
            this.finalPose = ack.finalPose;
            this.reason = null;
            this.status = MoveStatus.COMPLETED;
          } else {
            this.finalPose = null;
            this.reason = ack.reason;
            this.status = MoveStatus.FAILED;
          }
        })
        .catch((err) => {
          if (this.cancelled) return;
          this.finalPose = null;
          this.reason = (err as Error)?.message ?? String(err);
          this.status = MoveStatus.FAILED;
        })
        .finally(() => {
          resolve();
          if (this.endAtMs === null) this.endAtMs = Date.now();
        });
    });

    return this.taskPromise;
  }

  cancel() {
    if (
      this.status === MoveStatus.COMPLETED ||
      this.status === MoveStatus.FAILED
    ) {
      return;
    }
    this.cancelled = true;
    this.finalPose = null;
    this.reason = "Task was cancelled";
    this.status = MoveStatus.FAILED;
    this.endAtMs = Date.now();
  }
}

(() => {
  const CLEANUP_INTERVAL_MS = 15 * 60 * 1000; // 15 minute
  const INACTIVITY_TTL_MS = 5 * 60 * 1000; // 5 minutes

  setInterval(() => {
    MoveObjectTask.cleanupInactive(INACTIVITY_TTL_MS);
  }, CLEANUP_INTERVAL_MS);
})();

export function moveObject(options: {
  client: RealtimeClient;
  command: MoveCommand;
  timeoutMs?: number;
}) {
  const task = MoveObjectTask.create(options);
  task.execute();
  return task;
}

export default {};
