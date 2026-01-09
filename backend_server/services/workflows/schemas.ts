import type { generateText } from "ai";
import z from "zod";

import type { LanguageModel } from "ai";
export type { LanguageModel };

export const ObjectPropsSchema = z.object({
  object_name: z.string().min(1),
  object_description: z.string().optional().default(""),
});

export type ObjectProps = z.infer<typeof ObjectPropsSchema>;

export type GenerateTextOptions = Parameters<typeof generateText>[0];

export type ProviderOptions = Parameters<
  typeof generateText
>[0]["providerOptions"];

export type CodeExecutionOptions = { code: string; timeoutMs?: number };
