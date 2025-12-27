import createDebug from "debug";
import { z } from "zod";

const debug = createDebug("obj-g");



/** ===== Types ===== */
type Vec3 = { x: number; y: number; z: number };

type TransformDTO = {
    position: Vec3;
    rotationEuler: Vec3; // degrees
    scale: Vec3;
    space: "world" | "local";
};

type StateUpdateMsg = {
    type: "state_update";
    objectId: string;
    pose: TransformDTO;
    timestampMs?: number;
};

type AckMsg = {
    type: "ack";
    requestId: string;
    status: "ok" | "error";
    objectId?: string;
    message?: string;
};

type MoveCommand = {
    op: "move";
    objectId: string;
    to: TransformDTO;
    durationMs?: number;
};

type AttachCommand = {
    op: "attach";
    objectId: string;
    target: { kind: "hand"; hand: "left" | "right"; joint?: "palm" | "wrist" };
    offset?: TransformDTO; // local offset on hand
};

type CommandEnvelope = {
    type: "command";
    requestId: string;
    commands: Array<MoveCommand | AttachCommand>;
};

/** ===== In-memory state (backend 端記住 Unity 回報的最新座標) ===== */
const latestPoseById = new Map<string, TransformDTO>();
const latestTsById = new Map<string, number>();

/** ===== WS server ===== */



let unitySocket: WebSocket | null = null;



/** ===== Helpers ===== */
function rid(): string {
    return `req_${Math.random().toString(16).slice(2)}`;
}

function sendToUnity(envelope: CommandEnvelope): void {
    if (!unitySocket) {
        console.log("⚠️ Unity not connected, cannot send command.");
        return;
    }
    unitySocket.send(JSON.stringify(envelope));
}

/** ===== (1) 你要的：從 Unity 拿到茶壺座標（backend 端讀取） ===== */
export function getLatestPose(objectId: string): TransformDTO | null {
    return latestPoseById.get(objectId) ?? null;
}

/** ===== (2) 你要的：move function（產生封包 + 送 Unity） ===== */
export function moveObject(params: {
    objectId: string;
    toPosition: Vec3;
    toRotationEuler?: Vec3;
    toScale?: Vec3;
    durationMs?: number;
}): void {
    const envelope: CommandEnvelope = {
        type: "command",
        requestId: rid(),
        commands: [
            {
                op: "move",
                objectId: params.objectId,
                durationMs: params.durationMs ?? 300,
                to: {
                    position: params.toPosition,
                    rotationEuler: params.toRotationEuler ?? { x: 0, y: 0, z: 0 },
                    scale: params.toScale ?? { x: 1, y: 1, z: 1 },
                    space: "world"
                }
            }
        ]
    };
    sendToUnity(envelope);
}

/** ===== (2) 你要的：attach function（放到手上更好的做法） ===== */
export function attachToHand(params: {
    objectId: string;
    hand: "left" | "right";
    offset?: TransformDTO; // local offset on hand
}): void {
    const envelope: CommandEnvelope = {
        type: "command",
        requestId: rid(),
        commands: [
            {
                op: "attach",
                objectId: params.objectId,
                target: { kind: "hand", hand: params.hand, joint: "palm" },
                offset: params.offset ?? {
                    position: { x: 0.02, y: -0.02, z: 0.06 },
                    rotationEuler: { x: 0, y: 90, z: 0 },
                    scale: { x: 1, y: 1, z: 1 },
                    space: "local"
                }
            }
        ]
    };
    sendToUnity(envelope);
}

/** ===== Demo：測試用（你可以先看它有動） =====
 * 1) 每 3 秒把茶壺往 x +0.1 移動一次（前提：Unity 已生成 teapot_1 並開始 state_update）
 */
setInterval(() => {
    const pose = getLatestPose("teapot_1");
    if (!pose) return;

    moveObject({
        objectId: "teapot_1",
        toPosition: { x: pose.position.x + 0.1, y: pose.position.y, z: pose.position.z },
        toRotationEuler: pose.rotationEuler,
        durationMs: 400
    });
}, 3000);
