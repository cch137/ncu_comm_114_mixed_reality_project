import { EventEmitter } from "events";
import createDebug from "debug";
import { z } from "zod";
import { randomUUID } from "node:crypto";
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

export const RelativeMoveSchema = z.object({
    relative: z.object({
        deltaPos: z.tuple([z.number(), z.number(), z.number()]),
        deltaRot: z.tuple([z.number(), z.number(), z.number(), z.number()]).optional(),
    }),
});

export const MoveTargetSchema = z.union([PoseSchema, RelativeMoveSchema]);
export type MoveTarget = z.infer<typeof MoveTargetSchema>;

export const MoveCommandSchema = z.object({
    object_id: z.string().min(1),
    to: MoveTargetSchema,
    durationMs: z.number().int().min(0).max(60_000).optional(),
});

export type MoveCommand = z.infer<typeof MoveCommandSchema>;

// -------------------- Relative helpers --------------------
function isRelativeTarget(to: MoveTarget): to is z.infer<typeof RelativeMoveSchema> {
    return (to as any)?.relative?.deltaPos !== undefined;
}

function quatMultiply(
    a: [number, number, number, number],
    b: [number, number, number, number]
): [number, number, number, number] {
    const [ax, ay, az, aw] = a;
    const [bx, by, bz, bw] = b;

    const x = aw * bx + ax * bw + ay * bz - az * by;
    const y = aw * by - ax * bz + ay * bw + az * bx;
    const z = aw * bz + ax * by - ay * bx + az * bw;
    const w = aw * bw - ax * bx - ay * by - az * bz;

    return [x, y, z, w];
}

/**
 * ✅ 把相對位移轉成絕對 Pose
 * 注意：這裡吃的是 connection.ts 裡「Unity 上報 ObjectPose」的 cache。
 * 如果 cache 還沒有 pose，直接 throw（避免算錯亂飛）。
 */
export function resolveTargetPose(options: {
    client: RealtimeClient;
    objectId: string;
    to: MoveTarget;
}): Pose {
    const { client, objectId, to } = options;

    if (!isRelativeTarget(to)) {
        return to as Pose;
    }

    const current = client.getObjectPose(objectId);
    if (!current) {
        throw new Error(
            `No cached pose for object '${objectId}'. Unity must send ClientEvent.ObjectPose before relative move.`
        );
    }

    const [dx, dy, dz] = to.relative.deltaPos;
    const nextPos: [number, number, number] = [
        current.pos[0] + dx,
        current.pos[1] + dy,
        current.pos[2] + dz,
    ];

    let nextRot: [number, number, number, number] = current.rot;
    if (to.relative.deltaRot) {
        // 如果 Unity 定義「相對旋轉」乘法順序不同，這行可能要換成 quatMultiply(to.relative.deltaRot, current.rot)
        nextRot = quatMultiply(current.rot, to.relative.deltaRot);
    }

    return { pos: nextPos, rot: nextRot };
}

// -------------------- Task / State --------------------
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

    // ✅ 同一 object FIFO queue：promise tail
    private static readonly objectQueueTail = new Map<string, Promise<void>>();

    private static enqueueForObject(objectId: string) {
        const prevTail = this.objectQueueTail.get(objectId) ?? Promise.resolve();
        const prevSafe = prevTail.catch(() => {});

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

        const newTail = prevSafe.then(() => current);
        this.objectQueueTail.set(objectId, newTail);

  cancel() {
    if (
      this.status === MoveStatus.COMPLETED ||
      this.status === MoveStatus.FAILED
    ) {
      return;
    }
}

(() => {
    if (cleanupTimerStarted) return;
    cleanupTimerStarted = true;

    const CLEANUP_INTERVAL_MS = 15 * 60 * 1000; // 15 minute
    const INACTIVITY_TTL_MS = 5 * 60 * 1000; // 5 minutes

    const timer = setInterval(() => {
        MoveObjectTask.cleanupInactive(INACTIVITY_TTL_MS);
    }, CLEANUP_INTERVAL_MS);

    timer.unref?.();
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
