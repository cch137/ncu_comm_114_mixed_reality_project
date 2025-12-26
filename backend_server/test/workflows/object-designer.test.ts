// test/workflows/object-designer.test.ts
import path from "path";
import { mkdir, readFile, writeFile, stat } from "fs/promises";
import {
  extractCodeFromMarkdown,
  executeCodeAndExportGltf,
} from "../../services/workflows/object-designer";

describe("workflows/object-designer", () => {
  it("extracts code from teapot.md and exports GLTF to output file", async () => {
    jest.setTimeout(30_000);

    const fixturePath = path.resolve(
      process.cwd(),
      "test",
      "workflows",
      "fixtures",
      "teapot.md"
    );

    const md = await readFile(fixturePath, "utf8");
    const code = extractCodeFromMarkdown(md);

    expect(typeof code).toBe("string");
    expect(code.trim().length).toBeGreaterThan(0);

    const gltf = await executeCodeAndExportGltf({ code });
    expect(gltf).toBeTruthy();
    expect(typeof gltf).toBe("object");

    const outDir = path.resolve(
      process.cwd(),
      "public",
      "output",
      "workflows",
      "object-designer"
    );
    const outFile = path.join(outDir, "teapot.gltf");

    await mkdir(outDir, { recursive: true });
    await writeFile(outFile, JSON.stringify(gltf, null, 2), "utf8");

    const s = await stat(outFile);
    expect(s.isFile()).toBe(true);
    expect(s.size).toBeGreaterThan(0);
  });
});
