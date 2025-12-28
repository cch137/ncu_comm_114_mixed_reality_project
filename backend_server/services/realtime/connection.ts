import debug from "debug";
import { WSEvents, WSContext } from "hono/ws";
import { z } from "zod";
import { serverAudioPlayer } from "./audio";
import {
  AnchorState,
  EntityController,
  EntityState,
  EntityStateUpdateEvent,
} from "./entities";
import { ProtectedTinyNotifier } from "../../lib/utils/tiny-notifier";
import { generateRandomId } from "../../lib/utils/generate-random-id";

const DEFAULT_HEARTBEAT_MS = 10_000;

const log = debug("rltm");

/** These events are sent from the SERVER to the CLIENT. */
export enum ServerEvent {
  Error = "Error",
  Ping = "Ping",
  Pong = "Pong",
  LoadGLTF = "LoadGLTF",
  Audio = "Audio",
  JoinRoomOK = "JoinRoomOK",
  JoinRoomError = "JoinRoomError",
  LeaveRoomOK = "LeaveRoomOK",
  LeaveRoomError = "LeaveRoomError",
}

/** These events are sent from the CLIENT to the SERVER. */
export enum ClientEvent {
  Ping = "Ping",
  Pong = "Pong",
  StartAudio = "StartAudio",
  Audio = "Audio",
  EndAudio = "EndAudio",
  LoadGLTFOK = "LoadGLTFOK",
  Poses = "Poses",
  HeadPose = "HeadPose",
  HandsPose = "HandsPose",
  ClaimEntity = "ClaimEntity",
  UpdateEntity = "UpdateEntity",
  ReleaseEntity = "ReleaseEntity",
  JoinRoom = "JoinRoom",
  LeaveRoom = "LeaveRoom",
}

export type RealtimeClientOptions = {
  heartbeatMs?: number;
};

export const PoseSchema = z.object({
  pos: z.tuple([z.number(), z.number(), z.number()]),
  rot: z.tuple([z.number(), z.number(), z.number(), z.number()]),
});

const ItemIdPacketSchema = z.object({
  id: z.string(),
});

const AudioPacketSchema = z.object({
  pcm: z.string(),
});

const PosesPacket = z.tuple([PoseSchema, PoseSchema, PoseSchema]);

const HeadPosePacket = PoseSchema;

const HandsPoseSchema = z.tuple([PoseSchema, PoseSchema]);

const EntityUpdatePacketSchema = ItemIdPacketSchema.extend({
  pose: PoseSchema,
});

export type Pose = z.infer<typeof PoseSchema>;

class ScheduledTask {
  constructor(public readonly timeout: NodeJS.Timeout) {}
}

enum RealtimeClientAnchorType {
  Head,
  LeftHand,
  RightHand,
}

const CLIENT_ROOM = Symbol();

export class RealtimeClient extends ProtectedTinyNotifier<{
  type: RealtimeClientAnchorType;
  state: EntityStateUpdateEvent;
}> {
  private static _internalCounter = 0;

  private _ws?: WSContext<WebSocket>;
  readonly id = generateRandomId();
  name = `Client_${++RealtimeClient._internalCounter}`;

  protected readonly heartbeatMs: number;
  protected readonly intervals = new Set<ScheduledTask>();
  protected readonly timeouts = new Set<ScheduledTask>();
  protected readonly loadedObjectIds = new Set<string>();

  readonly controller = new EntityController();

  // 我們把頭、雙手看作是 anchor，它是一個 entity，
  // 在加入場景時會綁定到場景的 entity 管理。
  readonly head = new AnchorState();
  readonly leftHand = new AnchorState();
  readonly rightHand = new AnchorState();

  protected lastReceivedAtMs: number = Date.now();

  constructor(options: RealtimeClientOptions = {}) {
    super();
    this.heartbeatMs = options.heartbeatMs ?? DEFAULT_HEARTBEAT_MS;
    if (
      !(
        this.head.claim(this.controller) &&
        this.leftHand.claim(this.controller) &&
        this.rightHand.claim(this.controller)
      )
    ) {
      throw new Error("Failed to claim body parts");
    }
  }

  private get ws() {
    if (!this._ws) throw new Error("Client is not initialized");
    return this._ws;
  }

  initialize(ws: WSContext<WebSocket>) {
    if (this._ws) throw new Error("Client is already initialized");
    // 1 = OPEN ready state
    if (ws.readyState !== 1) throw new Error("Client is not in the OPEN state");
    this._ws = ws;
    log(`client [${this.id}] joined`);
    this.scheduleInterval(() => this.send(ServerEvent.Ping), this.heartbeatMs);
  }

  scheduleTimeout(cb: () => void, timeoutMs: number) {
    const task = new ScheduledTask(
      setTimeout(() => {
        this.timeouts.delete(task);
        cb();
      }, timeoutMs)
    );
    this.timeouts.add(task);
  }

  scheduleInterval(cb: () => void, intervalMs: number) {
    const task = new ScheduledTask(setInterval(() => cb(), intervalMs));
    this.intervals.add(task);
  }

  destroy() {
    try {
      this.ws.close();
    } catch {
      // ignore force closing error
    }
    log(`client [${this.id}] leaved`);
    this.intervals.forEach((i) => clearInterval(i.timeout));
    this.timeouts.forEach((i) => clearTimeout(i.timeout));
    if (this[CLIENT_ROOM]) RealtimeRoom.leave(this[CLIENT_ROOM]?.id, this);
  }

  send(type: ServerEvent, data?: any) {
    if (!this._ws?.raw || this._ws.raw.readyState !== this._ws.raw.OPEN) {
      log("cancel sending to [${this.id}] ${this.name}: ${type}");
      return;
    }
    try {
      this.ws.send(JSON.stringify({ type, data }));
      log(`sending to [${this.id}] ${this.name}: ${type}`);
    } catch (err) {
      log(`sending failed to [${this.id}] ${this.name}: ${type}]`);
    }
  }

  sendError(message: string) {
    this.send(ServerEvent.Error, { message });
    log(`error occured at [${this.id}]:`, message);
  }

  [CLIENT_ROOM]: RealtimeRoom | null = null;

  joinRoom(id: string) {
    return RealtimeRoom.join(id, this);
  }

  leaveRoom(id: string) {
    return RealtimeRoom.leave(id, this);
  }

  handleMessage(type: ClientEvent | string, data: unknown) {
    log(`client [${this.id}]: (${type})`);
    this.lastReceivedAtMs = Date.now();
    switch (type) {
      case ClientEvent.Poses: {
        try {
          const [headPose, leftHandPose, rightHandPose] =
            PosesPacket.parse(data);
          this.head.applyPose(this.controller, headPose);
          this.leftHand.applyPose(this.controller, leftHandPose);
          this.rightHand.applyPose(this.controller, rightHandPose);
        } catch {
          this.sendError("invalid poses");
        }
        break;
      }
      case ClientEvent.HeadPose: {
        try {
          const pose = HeadPosePacket.parse(data);
          this.head.applyPose(this.controller, pose);
        } catch {
          this.sendError("invalid head pose");
        }
        break;
      }
      case ClientEvent.HandsPose: {
        try {
          const [leftHandPose, rightHandPose] = HandsPoseSchema.parse(data);
          this.leftHand.applyPose(this.controller, leftHandPose);
          this.rightHand.applyPose(this.controller, rightHandPose);
        } catch {
          this.sendError("invalid hand poses");
        }
        break;
      }
      case ClientEvent.Ping: {
        this.send(ServerEvent.Pong);
        break;
      }
      case ClientEvent.Pong: {
        break;
      }
      case ClientEvent.Audio: {
        try {
          const { pcm } = AudioPacketSchema.parse(data);
          // TODO: serverAudioPlayer 是暫時性的測試，需要這裡的 pcm 傳給 realtime ai
          serverAudioPlayer.play(Buffer.from(pcm, "base64"));
        } catch (e) {
          this.sendError("invalid params at Audio");
        }
        break;
      }
      case ClientEvent.LoadGLTFOK: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          this.loadedObjectIds.add(id);
        } catch {
          this.sendError("invalid params at LoadGLTFOK");
        }
        break;
      }
      case ClientEvent.ClaimEntity: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          if (!this[CLIENT_ROOM]?.claimEntityById(this.controller, id)) {
            this.sendError(`Failed to claim entity "${id}"`);
          }
        } catch {
          this.sendError("invalid params at ClaimEntity");
        }
        break;
      }
      case ClientEvent.UpdateEntity: {
        try {
          const { id, pose } = EntityUpdatePacketSchema.parse(data);
          if (
            !this[CLIENT_ROOM]?.updateEntityById(this.controller, id, { pose })
          ) {
            this.sendError(`Failed to update entity "${id}"`);
          }
        } catch {
          this.sendError("invalid params at UpdateEntity");
        }
        break;
      }
      case ClientEvent.ReleaseEntity: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          if (!this[CLIENT_ROOM]?.releaseEntityById(this.controller, id)) {
            this.sendError(`Failed to release entity "${id}"`);
          }
        } catch {
          this.sendError("invalid params at ReleaseEntity");
        }
        break;
      }
      case ClientEvent.JoinRoom: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          this.joinRoom(id);
          this.send(ServerEvent.JoinRoomOK, { id });
        } catch {
          this.send(ServerEvent.JoinRoomError, { reason: "invalid params" });
        }
        break;
      }
      case ClientEvent.LeaveRoom: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          this.leaveRoom(id);
          this.send(ServerEvent.LeaveRoomOK, { id });
        } catch {
          this.send(ServerEvent.LeaveRoomError, { reason: "invalid params" });
        }
        break;
      }
      default: {
      }
    }
  }
}

class RealtimeRoom {
  private static readonly rooms = new Map<string, RealtimeRoom>();

  static get(id: string) {
    return RealtimeRoom.rooms.get(id);
  }

  static join(id: string, client: RealtimeClient) {
    let room = RealtimeRoom.get(id);
    if (!room) {
      room = new RealtimeRoom(id);
      RealtimeRoom.rooms.set(room.id, room);
    }
    room.addClient(client);
    return room;
  }

  static leave(id: string, client: RealtimeClient) {
    const room = RealtimeRoom.get(id);
    if (room) {
      const removed = room.removeClient(client);
      if (room.clients.size === 0) RealtimeRoom.rooms.delete(room.id);
      return removed;
    }
    return false;
  }

  protected readonly clients = new Set<RealtimeClient>();
  protected readonly entities = new Map<string, EntityState>();

  private constructor(public readonly id: string) {}

  private addClient(client: RealtimeClient) {
    if (this.clients.has(client)) return;
    const prevRoomId = client[CLIENT_ROOM]?.id ?? null;
    if (prevRoomId !== null) {
      RealtimeRoom.leave(prevRoomId, client);
    }
    this.clients.add(client);
    this.entities.set(client.head.id, client.head);
    this.entities.set(client.leftHand.id, client.leftHand);
    this.entities.set(client.rightHand.id, client.rightHand);
    client[CLIENT_ROOM] = this;
  }

  private removeClient(client: RealtimeClient) {
    if (client[CLIENT_ROOM] === this) client[CLIENT_ROOM] = null;
    this.entities.delete(client.head.id);
    this.entities.delete(client.leftHand.id);
    this.entities.delete(client.rightHand.id);
    this.entities.forEach((entity) => {
      // 只釋放目前受控制的 entity，對目前不受控制的 entity 沒有影響。
      client.controller.release(entity);
    });
    return this.clients.delete(client);
  }

  getEntities() {
    return Array.from(this.entities.values());
  }

  hasEntity(entityId: string): boolean;
  hasEntity(entity: EntityState): boolean;
  hasEntity(entity: string | EntityState): boolean {
    return this.entities.has(typeof entity === "object" ? entity.id : entity);
  }

  getEntityById(entityId: string) {
    return this.entities.get(entityId) ?? null;
  }

  addEntity(entity: EntityState) {
    this.entities.set(entity.id, entity);
  }

  removeEntity(entity: EntityState) {
    this.entities.delete(entity.id);
    this.clients.forEach((client) => entity.release(client.controller));
  }

  claimEntityById(controller: EntityController, entityId: string) {
    return this.entities.get(entityId)?.claim(controller) ?? false;
  }

  updateEntityById(
    controller: EntityController,
    entityId: string,
    { pose }: { pose: Pose }
  ) {
    return this.entities.get(entityId)?.applyPose(controller, pose) ?? false;
  }

  releaseEntityById(controller: EntityController, entityId: string) {
    return this.entities.get(entityId)?.release(controller) ?? false;
  }

  [Symbol.iterator]() {
    return this.clients[Symbol.iterator]();
  }
}

function isValidPacket(
  data: unknown
): data is { type: string; data?: unknown } {
  if (!data || typeof data !== "object") return false;
  if ("type" in data && typeof data.type === "string") return true;
  return false;
}

export function realtimeHandler(): WSEvents<WebSocket> {
  const rtClient = new RealtimeClient();

  return {
    onOpen(_event, ws) {
      rtClient.initialize(ws);
      rtClient.scheduleTimeout(() => {
        rtClient.send(ServerEvent.LoadGLTF, {
          id: generateRandomId(),
          name: "茶壺",
          url: "https://40001.cch137.com/output/workflows/object-designer/teapot.gltf",
        });
      }, 1_000);
    },

    async onMessage(event, _ws) {
      try {
        const parsed =
          typeof event.data === "string"
            ? JSON.parse(event.data)
            : event.data instanceof Blob
            ? JSON.parse(await event.data.text())
            : event.data instanceof ArrayBuffer
            ? JSON.parse(new TextDecoder().decode(event.data))
            : null;
        if (!isValidPacket(parsed)) {
          return log(`client [${rtClient.id}] sent an invalid packet.`);
        }
        rtClient.handleMessage(parsed.type, parsed.data);
      } catch {
        return log(`client [${rtClient.id}] sent unparsable packet.`);
      }
    },

    onClose(_event, _ws) {
      rtClient.destroy();
    },
  };
}
