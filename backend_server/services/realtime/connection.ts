import debug from "debug";
import { WSEvents, WSContext } from "hono/ws";
import { z } from "zod";
import { serverAudioPlayer } from "./audio";
import {
  AnchorState,
  EntityController,
  EntityState,
  EntityStateUpdateEvent,
  EntityStateUpdateEventType,
  EntityType,
  ProgrammableObjectState,
} from "./entities";
import { ProtectedTinyNotifier } from "../../lib/utils/tiny-notifier";
import { generateRandomId } from "../../lib/utils/generate-random-id";

const DEFAULT_HEARTBEAT_MS = 10_000;

const log = debug("rltm");

export enum ServerEvent {
  Error = "Error",
  Ping = "Ping",
  Pong = "Pong",
  CreateEntityProgObj = "CreateEntityProgObj",
  CreateEntityGeomObj = "CreateEntityGeomObj",
  CreateEntityAnchor = "CreateEntityAnchor",
  UpdateEntity = "UpdateEntity",
  DelEntity = "DelEntity",
  Audio = "Audio",
  JoinRoomOK = "JoinRoomOK",
  JoinRoomError = "JoinRoomError",
  LeaveRoomOK = "LeaveRoomOK",
  LeaveRoomError = "LeaveRoomError",
}

export enum ClientEvent {
  Ping = "Ping",
  Pong = "Pong",
  StartAudio = "StartAudio",
  Audio = "Audio",
  EndAudio = "EndAudio",
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
  // 在未來這個屬性應該允許被更改，但是目前它是 readonly 的狀態。
  readonly name = `Client_${++RealtimeClient._internalCounter}`;

  protected readonly heartbeatMs: number;
  protected readonly intervals = new Set<ScheduledTask>();
  protected readonly timeouts = new Set<ScheduledTask>();

  readonly controller = new EntityController();

  // 我們把頭、雙手都看作是 anchor，各是一個 entity，
  // 在加入場景時會綁定到場景的 entity 管理。
  readonly head = new AnchorState();
  readonly leftHand = new AnchorState();
  readonly rightHand = new AnchorState();

  private isDestroyed = false;
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
    if (this._ws) {
      if (this._ws === ws) return;
      throw new Error("Client is already initialized");
    }
    // 1 = OPEN ready state
    if (ws.readyState !== 1) throw new Error("Client is not in the OPEN state");
    this._ws = ws;
    log(`client [${this.id}] connected`);
    this.scheduleInterval(() => this.send(ServerEvent.Ping), this.heartbeatMs);
  }

  scheduleTimeout(cb: () => void, timeoutMs: number) {
    if (this.isDestroyed) throw new Error("Client is destroyed");
    const task = new ScheduledTask(
      setTimeout(() => {
        this.timeouts.delete(task);
        cb();
      }, timeoutMs)
    );
    this.timeouts.add(task);
  }

  scheduleInterval(cb: () => void, intervalMs: number) {
    if (this.isDestroyed) throw new Error("Client is destroyed");
    const task = new ScheduledTask(setInterval(() => cb(), intervalMs));
    this.intervals.add(task);
  }

  destroy() {
    this.isDestroyed = true;
    try {
      this.ws.close();
    } catch {
      // ignore force closing error
    }
    log(`client [${this.id}] disconnected`);
    this.intervals.forEach((i) => clearInterval(i.timeout));
    this.timeouts.forEach((i) => clearTimeout(i.timeout));
    if (this[CLIENT_ROOM]) RealtimeRoom.leave(this[CLIENT_ROOM]?.id, this);
  }

  private send(type: ServerEvent, data?: any) {
    const raw = this._ws?.raw;
    if (!raw || raw.readyState !== 1 /* WebSocket.OPEN */) {
      log(`send skipped: client [${this.id}] ${this.name}: ${type}`);
      return;
    }
    try {
      this.ws.send(JSON.stringify({ type, data }));
      log(`message sent: client [${this.id}] ${this.name}: ${type}`);
    } catch (err) {
      log(`send failed: client [${this.id}] ${this.name}: ${type}`);
    }
  }

  private sendError(message: string) {
    this.send(ServerEvent.Error, { message });
    log(`error: client [${this.id}]: ${message}`);
  }

  [CLIENT_ROOM]: RealtimeRoom | null = null;

  isBodyPart(entity: EntityState) {
    return (
      entity === this.head ||
      entity === this.leftHand ||
      entity === this.rightHand
    );
  }

  /** 在 Client 端建立一個 Entity */
  createEntity(entity: EntityState) {
    try {
      if (!this[CLIENT_ROOM]?.hasEntity(entity)) return;
      if (this.isBodyPart(entity)) return;
      switch (entity.type) {
        case EntityType.ProgrammableObject: {
          this.send(ServerEvent.CreateEntityProgObj, {
            id: entity.id,
            pose: entity.pose,
            gltf: { name: entity.props.object_name, url: entity.url },
          });
          break;
        }
        case EntityType.GeometryObject: {
          this.send(ServerEvent.CreateEntityGeomObj, {
            id: entity.id,
            pose: entity.pose,
          });
          break;
        }
        case EntityType.Anchor: {
          this.send(ServerEvent.CreateEntityAnchor, {
            id: entity.id,
            pose: entity.pose,
          });
          break;
        }
      }
    } catch (err) {
      log(`create entity error: client [${this.id}]: ${err}`);
    }
  }

  /** 在 Client 更新一個 Entity 的狀態 */
  updateEntity(entity: EntityState) {
    try {
      if (!this[CLIENT_ROOM]?.hasEntity(entity)) return;
      if (this.isBodyPart(entity)) return;
      this.send(ServerEvent.UpdateEntity, {
        id: entity.id,
        pose: entity.pose,
      });
      return;
    } catch (err) {
      log(`update entity error: client [${this.id}]: ${err}`);
    }
  }

  /** 在 Client 刪除一個 Entity */
  dropEntity(entity: EntityState) {
    try {
      if (!this[CLIENT_ROOM]?.hasEntity(entity)) return;
      if (this.isBodyPart(entity)) return;
      this.send(ServerEvent.DelEntity, { id: entity.id });
      // 在未取得控制權的情況下，此處的調用不會對既有的控制者或目標實體產生任何影響。
      this.controller.release(entity);
    } catch (err) {
      log(`drop entity error: client [${this.id}]: ${err}`);
    }
  }

  handleMessage(type: ClientEvent | string, data: unknown) {
    log(`message received: client [${this.id}]: ${type}`);
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
          this.sendError("invalid Audio params");
        }
        break;
      }
      case ClientEvent.ClaimEntity: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          if (!this[CLIENT_ROOM]?.claimEntityById(this.controller, id)) {
            this.sendError(`failed to claim entity "${id}"`);
          }
        } catch {
          this.sendError("invalid ClaimEntity params");
        }
        break;
      }
      case ClientEvent.UpdateEntity: {
        try {
          const { id, pose } = EntityUpdatePacketSchema.parse(data);
          if (
            !this[CLIENT_ROOM]?.updateEntityById(this.controller, id, { pose })
          ) {
            this.sendError(`failed to update entity "${id}"`);
          }
        } catch {
          this.sendError("invalid UpdateEntity params");
        }
        break;
      }
      case ClientEvent.ReleaseEntity: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          if (!this[CLIENT_ROOM]?.releaseEntityById(this.controller, id)) {
            this.sendError(`failed to release entity "${id}"`);
          }
        } catch {
          this.sendError("invalid ReleaseEntity params");
        }
        break;
      }
      case ClientEvent.JoinRoom: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          RealtimeRoom.join(id, this);
          this.send(ServerEvent.JoinRoomOK, { id });
        } catch {
          this.send(ServerEvent.JoinRoomError, { reason: "invalid params" });
        }
        break;
      }
      case ClientEvent.LeaveRoom: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          RealtimeRoom.leave(id, this);
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

export class RealtimeRoom {
  private static readonly rooms = new Map<string, RealtimeRoom>();

  static get(id: string) {
    return RealtimeRoom.rooms.get(id);
  }

  static join(id: string, client: RealtimeClient) {
    let room = RealtimeRoom.get(id);
    if (!room) {
      // 當創建新房間時，我們為他們添加一個破茶壺。
      const teapot = new ProgrammableObjectState({
        props: {
          object_name: "Teapot",
          object_description: "A default object used for testing.",
        },
        url: "https://40001.cch137.com/output/workflows/object-designer/teapot.gltf",
      });
      room = new RealtimeRoom(id);
      room.addEntity(teapot);
      RealtimeRoom.rooms.set(room.id, room);
    }
    room.addClient(client);
    return room;
  }

  static leave(id: string, client: RealtimeClient) {
    const room = RealtimeRoom.get(id);
    if (room) {
      room.removeClient(client);
      if (room.clients.size === 0) RealtimeRoom.rooms.delete(room.id);
    }
  }

  protected readonly clients = new Set<RealtimeClient>();
  protected readonly entities = new Map<string, EntityState>();
  protected readonly entitiesUpdateCallbacks = new WeakMap<
    EntityState,
    (state: EntityStateUpdateEvent) => void
  >();

  private constructor(public readonly id: string) {}

  private addClient(client: RealtimeClient) {
    if (this.clients.has(client)) return;
    const prevRoomId = client[CLIENT_ROOM]?.id ?? null;
    if (prevRoomId !== null) {
      RealtimeRoom.leave(prevRoomId, client);
    }
    client[CLIENT_ROOM] = this;
    this.clients.add(client);
    this.addEntity(client.head);
    this.addEntity(client.leftHand);
    this.addEntity(client.rightHand);
    this.entities.forEach((entity) => client.createEntity(entity));
  }

  private removeClient(client: RealtimeClient) {
    this.clients.delete(client);
    this.removeEntity(client.head);
    this.removeEntity(client.leftHand);
    this.removeEntity(client.rightHand);
    this.entities.forEach((entity) => client.dropEntity(entity));
    // 清空房間一定要放到最後才做
    if (client[CLIENT_ROOM] === this) client[CLIENT_ROOM] = null;
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
    if (this.entities.has(entity.id)) return;
    this.entities.set(entity.id, entity);
    const cb = (state: EntityStateUpdateEvent) => {
      if (state.type === EntityStateUpdateEventType.Pose) {
        for (const c of this.clients) c.updateEntity(entity);
      }
    };
    this.entitiesUpdateCallbacks.set(entity, cb);
    entity.subscribe(cb);
    for (const c of this.clients) c.createEntity(entity);
  }

  removeEntity(entity: EntityState) {
    this.clients.forEach((client) => client.dropEntity(entity));
    const cb = this.entitiesUpdateCallbacks.get(entity);
    if (cb) entity.unsubscribe(cb);
    this.entitiesUpdateCallbacks.delete(entity);
    this.entities.delete(entity.id);
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

const clientsWeakMap = new WeakMap<WSContext<WebSocket>, RealtimeClient>();

export function realtimeHandler(): WSEvents<WebSocket> {
  let client: RealtimeClient | null = null;

  return {
    onOpen(_event, ws) {
      client = clientsWeakMap.get(ws) ?? null;
      if (!client) {
        client = new RealtimeClient();
        clientsWeakMap.set(ws, client);
      }
      client.initialize(ws);
    },

    async onMessage(event, ws) {
      client ??= clientsWeakMap.get(ws) ?? null;
      if (!client) return log("client was unexpectedly not found");
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
          return log(`invalid packet received: client [${client.id}]`);
        }
        client.handleMessage(parsed.type, parsed.data);
      } catch {
        return log(`unparsable packet received: client [${client.id}]`);
      }
    },

    onClose(_event, ws) {
      client ??= clientsWeakMap.get(ws) ?? null;
      if (!client) return log("client was unexpectedly not found");
      client.destroy();
    },
  };
}
