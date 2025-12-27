import createDebug from "debug";
import type { WSEvents, WSContext } from "hono/ws";
import { z } from "zod";

const debug = createDebug("rltm");

function generateRandomId(): string {
  const hex = crypto.randomUUID().replace(/-/g, "");
  const bytes = new Uint8Array(
    hex.match(/.{1,2}/g)!.map((byte) => parseInt(byte, 16))
  );
  const base64 = btoa(String.fromCharCode(...bytes));
  return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** These events are sent from the SERVER to the CLIENT. */
export enum ServerEvent {
  Error = "Error",
  Ping = "Ping",
  Pong = "Pong",
  LoadGLTF = "LoadGLTF",
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
  HeadPose = "HeadPose",
  HandsPose = "HandsPose",
  JoinRoom = "JoinRoom",
  LeaveRoom = "LeaveRoom",
}

export type RealtimeClientOptions = {
  heartbeatMs?: number;
};

const PoseSchema = z.object({
  pos: z.tuple([z.number(), z.number(), z.number()]),
  rot: z.tuple([z.number(), z.number(), z.number(), z.number()]),
});

const ItemIdPacketSchema = z.object({
  id: z.string(),
});

const HeadPosePacket = PoseSchema;

const HandsPoseSchema = z.tuple([PoseSchema, PoseSchema]);

export type Pose = z.infer<typeof PoseSchema>;

export class RealtimeClient {
  private static _internalCounter = 0;

  private _ws?: WSContext<WebSocket>;
  readonly id = generateRandomId();
  name = `Client_${++RealtimeClient._internalCounter}`;

  protected readonly heartbeatMs: number;
  protected readonly intervals: NodeJS.Timeout[] = [];
  protected readonly loadedObjectIds = new Set<string>();

  headPose: Pose = { pos: [0, 0, 0], rot: [0, 0, 0, 0] };
  leftHandPose: Pose = { pos: [0, 0, 0], rot: [0, 0, 0, 0] };
  rightHandPose: Pose = { pos: [0, 0, 0], rot: [0, 0, 0, 0] };

  protected lastReceivedAtMs: Number = NaN;

  constructor(options: RealtimeClientOptions = {}) {
    this.heartbeatMs = options.heartbeatMs ?? 10_000;
  }

  get ws() {
    if (!this._ws) throw new Error("Client is not initialized");
    return this._ws;
  }

  get isConnected() {
    return this.ws.raw?.readyState === this.ws.raw?.OPEN;
  }

  get isConnecting() {
    return this.ws.raw?.readyState === this.ws.raw?.CONNECTING;
  }

  get isClosing() {
    return this.ws.raw?.readyState === this.ws.raw?.CLOSING;
  }

  get isClosed() {
    return this.ws.raw?.readyState === this.ws.raw?.CLOSED;
  }

  initialize(ws: WSContext<WebSocket>) {
    if (this._ws) throw new Error("Client is already initialized");
    if (this.isClosing || this.isClosed)
      throw new Error("Client is closing or closed");
    debug(`client [${this.id}] joined`);
    this._ws = ws;
    this.scheduleInterval(() => this.send(ServerEvent.Ping), this.heartbeatMs);
  }

  scheduleTimeout(cb: () => void, timeoutMs: number) {
    this.intervals.push(setTimeout(cb, timeoutMs));
  }

  scheduleInterval(cb: () => void, intervalMs: number) {
    this.intervals.push(setInterval(cb, intervalMs));
  }

  release() {
    debug(`client [${this.id}] leaved`);
    this.intervals.forEach(clearInterval);
    if (this.room) RealtimeRoom.leave(this.room?.id, this);
    this.ws.close();
  }

  send(type: ServerEvent, data?: any) {
    this.ws.send(JSON.stringify({ type, data }));
    debug(`sending to [${this.id}] ${this.name}: ${type}`);
  }

  protected room: RealtimeRoom | null = null;

  joinRoom(id: string) {
    this.room?.removeClient(this);
    this.room = RealtimeRoom.join(id, this);
  }

  leaveRoom(id: string) {
    RealtimeRoom.leave(id, this);
    this.room = null;
  }

  handleMessage(type: ClientEvent | string, data: unknown) {
    debug(`client [${this.id}]: (${type})`, data);
    this.lastReceivedAtMs = Date.now();
    switch (type) {
      case ClientEvent.HeadPose: {
        try {
          const pose = HeadPosePacket.parse(data);
          this.headPose = pose;
        } catch {
          this.send(ServerEvent.Error, { message: "invalid head pose" });
        }
        break;
      }
      case ClientEvent.HandsPose: {
        try {
          const [leftHandPose, rightHandPose] = HandsPoseSchema.parse(data);
          this.leftHandPose = leftHandPose;
          this.rightHandPose = rightHandPose;
        } catch {
          this.send(ServerEvent.Error, { message: "invalid hand poses" });
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
      case ClientEvent.LoadGLTFOK: {
        try {
          const { id } = ItemIdPacketSchema.parse(data);
          this.loadedObjectIds.add(id);
        } catch {
          this.send(ServerEvent.Error, {
            message: "invalid params at LoadGLTFOK",
          });
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
    const room = RealtimeRoom.get(id);
    if (room) {
      room.addClient(client);
      return room;
    } else {
      const room = new RealtimeRoom(id);
      RealtimeRoom.rooms.set(room.id, room);
      room.addClient(client);
      return room;
    }
  }

  static leave(id: string, client: RealtimeClient) {
    const room = RealtimeRoom.get(id);
    if (room) {
      const removed = room.removeClient(client);
      if (room.clients.size === 0) RealtimeRoom.rooms.delete(room.id);
      return removed;
    }
    RealtimeRoom.get(id)?.removeClient(client);
  }

  private readonly clients = new Set<RealtimeClient>();

  private constructor(public readonly id: string) {}

  addClient(client: RealtimeClient) {
    this.clients.add(client);
  }

  removeClient(client: RealtimeClient) {
    return this.clients.delete(client);
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
      rtClient.scheduleInterval(() => {
        ws.send(
          JSON.stringify({
            type: "LoadGLTF",
            data: {
              id: generateRandomId(),
              name: "茶壺",
              url: "https://40001.cch137.com/public/output/workflows/object-designer/teapot.gltf",
            },
          })
        );
      }, 5_000);
    },

    async onMessage(event, _ws) {
      const parsed =
        typeof event.data === "string"
          ? JSON.parse(event.data)
          : event.data instanceof Blob
          ? JSON.parse(await event.data.text())
          : event.data instanceof ArrayBuffer
          ? JSON.parse(new TextDecoder().decode(event.data))
          : null;
      if (!isValidPacket(parsed)) {
        return debug(`client [${rtClient.id}] sent an invalid packet.`);
      }
      rtClient.handleMessage(parsed.type, parsed.data);
    },

    onClose(_event, _ws) {
      rtClient.release();
    },
  };
}
