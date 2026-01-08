import BetterSqlite3 from "better-sqlite3";
import debug from "debug";
import fs from "fs";
import path from "path";
import { stringifyError } from "../../lib/utils/error-handle";

const log = debug("db");

const QUERIES_DIRNAME = path.resolve(__dirname, "queries");
const MIGRATIONS_DIRNAME = path.resolve(__dirname, "migrations");
const DATABASE_DIRNAME = path.resolve(__dirname, "data");

function readSql(sqlFile: string, migration = false) {
  const filepath = path.join(
    migration ? MIGRATIONS_DIRNAME : QUERIES_DIRNAME,
    `${sqlFile}.sql`
  );
  if (!fs.existsSync(filepath)) {
    throw new Error(`Database SQL not found: ${filepath}`);
  }
  return fs.readFileSync(filepath, "utf8");
}

async function sha256(data: string | Uint8Array) {
  const array = Uint8Array.from(
    typeof data === "string" ? new TextEncoder().encode(data) : data
  );
  const buf = await crypto.subtle.digest("SHA-256", array);
  const hex = [...new Uint8Array(buf)]
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");

  return hex;
}

export type AnyFn = (...args: any[]) => any;

export type QueryFactoryContext<DB, K extends string> = {
  db: DB;
  name: K; // function name, e.g. "Initialize"
  sqlFilename: string;
  sql: string;
};

export type QuerySpec<DB, K extends string, F extends AnyFn> =
  | {
      file?: string;
      migration?: true;
      build: (ctx: QueryFactoryContext<DB, K>) => F;
    }
  | ((ctx: QueryFactoryContext<DB, K>) => F);

export type QuerySpecs<M extends Record<string, AnyFn>, DB> = {
  [K in keyof M & string]: QuerySpec<DB, K, M[K]>;
};

export class Database<M extends Record<string, AnyFn>> extends BetterSqlite3 {
  public readonly queries: M;
  public readonly dirname: string;

  constructor(
    public readonly dbName: string,
    specs: QuerySpecs<M, Database<M>>,
    options?: BetterSqlite3.Options
  ) {
    const dirname = path.join(DATABASE_DIRNAME, dbName);
    const filepath = path.join(dirname, "./db.sqlite3");
    const built: M = Object.create(null);

    fs.mkdirSync(dirname, { recursive: true });

    super(filepath, options);

    for (const name in specs) {
      const spec = specs[name];
      const {
        file: sqlFilename = name,
        migration,
        build,
      } = typeof spec === "function" ? { build: spec } : spec;
      const sql = readSql(sqlFilename, migration);

      built[name as keyof M] = build({ db: this, name, sqlFilename, sql });
    }

    this.dirname = dirname;
    this.queries = built;
  }

  log(...args: any[]) {
    return log(`[${this.dbName}]`, ...args);
  }
}

export type QueryMap = {
  initialize: () => Promise<void>;

  add_result: (params: {
    task: {
      id: string;
      name: string;
      description: string;
    };
    result: {
      version: string;
      code?: string | null;
      error?: string | null;
      mime_type?: string | null;
      blob_content?: Buffer | Uint8Array | null;
    };
  }) => Promise<{ id: number }>;

  get_result: (params: { task_id: string; version: string }) => Promise<{
    id: number;
    task_id: string;
    version: string;
    code: string | null;
    error: string | null;
    mime_type: string | null;
    blob_content: Buffer | null;
    created_at: number;
  } | null>;

  get_latest_result: (params: { task_id: string }) => Promise<{
    id: number;
    task_id: string;
    version: string;
    code: string | null;
    error: string | null;
    mime_type: string | null;
    blob_content: Buffer | null;
    created_at: number;
  } | null>;

  get_result_list: (params: { task_id: string }) => Promise<
    Array<{
      version: string;
      success: boolean;
      error: string | null;
    }>
  >;

  get_result_code: (params: { task_id: string; version: string }) => Promise<{
    code: string | null;
    error: string | null;
  } | null>;

  get_result_content: (params: {
    task_id: string;
    version: string;
  }) => Promise<{
    mime_type: string | null;
    blob_content: Buffer | null;
    error: string | null;
  } | null>;
};

export const db = new Database<QueryMap>("main", {
  initialize: {
    file: "0001_initialization",
    migration: true,
    build:
      ({ db, sql }) =>
      async () => {
        db.pragma("journal_mode = WAL");
        db.pragma("foreign_keys = ON");
        db.pragma("busy_timeout = 5000");

        // We need to strictly verify the initialization SQL because schema differences can cause major issues;
        // other queries don’t need this level of checking.
        const checksum = await sha256(sql);
        const checksumFilepath = path.join(db.dirname, "checksum.sha256");

        if (fs.existsSync(checksumFilepath)) {
          const hash = await fs.promises.readFile(checksumFilepath, "utf8");
          if (hash === checksum) {
            db.log("database schema already initialized");
            return;
          }
          throw new Error("database schema checksum mismatch");
        }

        db.exec(sql);
        await fs.promises.writeFile(checksumFilepath, checksum);
        db.log("database schema initialized");
      },
  },

  add_result:
    ({ db, sql }) =>
    async ({ task, result }) => {
      // Enforce the table CHECK constraint expectations
      const mime = result.mime_type ?? null;
      const blob = result.blob_content ?? null;
      if ((mime === null) !== (blob === null)) {
        throw new Error(
          "mime_type and blob_content must both be null or both be non-null"
        );
      }

      const stmt = db.prepare(sql);

      const run = db.transaction(() => {
        const row = stmt.get({
          task_id: task.id,
          task_name: task.name,
          task_description: task.description,

          version: result.version,
          code: result.code ?? null,
          error: result.error ?? null,
          mime_type: mime,
          blob_content: blob,
        }) as { id: number } | undefined;

        if (!row) throw new Error("add_result: missing RETURNING id");
        return row;
      });

      return run();
    },

  get_result:
    ({ db, sql }) =>
    async ({ task_id, version }) => {
      const stmt = db.prepare(sql);
      const row = stmt.get({ task_id, version }) as
        | {
            id: number;
            task_id: string;
            version: string;
            code: string | null;
            error: string | null;
            mime_type: string | null;
            blob_content: Buffer | null;
            created_at: number;
          }
        | undefined;

      return row ?? null;
    },

  get_latest_result:
    ({ db, sql }) =>
    async ({ task_id }) => {
      const stmt = db.prepare(sql);
      const row = stmt.get({ task_id }) as
        | {
            id: number;
            task_id: string;
            version: string;
            code: string | null;
            error: string | null;
            mime_type: string | null;
            blob_content: Buffer | null;
            created_at: number;
          }
        | undefined;

      return row ?? null;
    },

  get_result_list:
    ({ db, sql }) =>
    async ({ task_id }) => {
      const stmt = db.prepare(sql);
      const rows = stmt.all({ task_id }) as Array<{
        version: string;
        success: 0 | 1;
        error: string | null;
      }>;

      return rows.map((r) => ({
        version: r.version,
        success: Boolean(r.success),
        error: r.error,
      }));
    },

  get_result_code:
    ({ db, sql }) =>
    async ({ task_id, version }) => {
      const stmt = db.prepare(sql);
      const row = stmt.get({ task_id, version }) as
        | { code: string | null; error: string | null }
        | undefined;

      return row ?? null;
    },

  get_result_content:
    ({ db, sql }) =>
    async ({ task_id, version }) => {
      const stmt = db.prepare(sql);
      const row = stmt.get({ task_id, version }) as
        | {
            mime_type: string | null;
            blob_content: Buffer | null;
            error: string | null;
          }
        | undefined;

      return row ?? null;
    },
});

db.queries.initialize().catch((err) => {
  log("initialization error:", stringifyError(err));
  process.exit(1);
});
