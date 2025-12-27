// test/workflows/object-designer.test.ts
import path from "path";
import { mkdir, readFile, writeFile, stat } from "fs/promises";
import { encode } from "cbor-x";
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
    const code = extractCodeFromMarkdown(md) ?? md;

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
    const outFile1 = path.join(outDir, "teapot.gltf");
    const outFile2 = path.join(outDir, "teapot.gltf.cbor");

    await mkdir(outDir, { recursive: true });
    await writeFile(outFile1, JSON.stringify(gltf, null, 2), "utf8");
    await writeFile(outFile2, encode(gltf), "binary");

    const s1 = await stat(outFile1);
    expect(s1.isFile()).toBe(true);
    expect(s1.size).toBeGreaterThan(0);

    const s2 = await stat(outFile2);
    expect(s2.isFile()).toBe(true);
    expect(s2.size).toBeGreaterThan(0);
  });
});
