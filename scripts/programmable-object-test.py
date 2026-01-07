from urllib import request
import time
import json

API_BASE_URL = "https://40001.cch137.com/obj-dsgn"


class ObjectMetadata:
    def __init__(self, task_id: str, object_name: str, object_description: str):
        self.id = task_id
        self.name = object_name
        self.description = object_description
        self.gltf = {}


def create_object_task() -> ObjectMetadata:
    object_name = "蒙娜麗莎的微笑"
    object_description = "蒙娜麗莎的微笑"

    req = request.Request(
        API_BASE_URL + "/tasks",
        method="POST",
        data=json.dumps(
            {
                "model": "gemini-3-flash-preview",
                "object_name": object_name,
                "object_description": object_description,
            }
        ).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )

    with request.urlopen(req) as resp:
        # Response schema: { "data": { "id": string } }
        task_id = json.loads(resp.read().decode("utf-8"))["data"]["id"]
        if not isinstance(task_id, str):
            raise TypeError("id is not string")
        return ObjectMetadata(task_id, object_name, object_description)


def poll_task_gltf(task_id: str):
    while True:
        req = request.Request(
            API_BASE_URL + "/tasks/" + task_id,
            method="GET",
        )

        with request.urlopen(req) as resp:
            # Response schema: { status: string, reason: string, gltf: object }
            task_state = json.loads(resp.read().decode("utf-8"))["data"]

            print("task status:", task_state["status"])
            if task_state["status"] == "completed":
                if not task_state["gltf"]:
                    raise Exception("gltf not found")
                return task_state["gltf"]

            if task_state["status"] == "failed":
                if not task_state["reason"]:
                    raise Exception("task failed, reason not found")
                raise Exception(task_state["reason"])

            # Wait 1 second before polling again
            time.sleep(1)


def debug_add_object_to_rooms(payload: dict):
    req = request.Request(
        API_BASE_URL + "/_debug_add_prog_obj_rooms",
        method="POST",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    with request.urlopen(req) as _:
        pass


if __name__ == "__main__":
    obj = create_object_task()
    obj.gltf = poll_task_gltf(obj.id)
    debug_add_object_to_rooms(
        {
            "props": {"object_name": obj.name, "object_description": obj.description},
            "url": API_BASE_URL + "/tasks/" + obj.id + "/gltf",
        }
    )
