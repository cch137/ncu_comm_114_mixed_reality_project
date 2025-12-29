import path from "path";
import { readFile } from "fs/promises";
import Handlebars from "handlebars";

export async function loadInstructionsTemplate<T = {}>(name: string) {
  const filePath = path.resolve(
    process.cwd(),
    "instructions",
    `threejs-${name}.md`
  );
  const md = await readFile(filePath, "utf8");
  return Handlebars.compile<T>(md);
}
