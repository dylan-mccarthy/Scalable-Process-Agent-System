"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useRouter } from "next/navigation";
import { useState } from "react";
import * as z from "zod";

import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import type { Agent, CreateAgentRequest, UpdateAgentRequest } from "@/types/api";
import { createAgent, updateAgent } from "@/lib/api";

/**
 * Available Azure OpenAI models for agent configuration
 */
const AVAILABLE_MODELS = [
  { value: "gpt-4", label: "GPT-4" },
  { value: "gpt-4-turbo", label: "GPT-4 Turbo" },
  { value: "gpt-4o", label: "GPT-4o" },
  { value: "gpt-35-turbo", label: "GPT-3.5 Turbo" },
] as const;

/**
 * Available Azure AI Foundry tools
 */
const AVAILABLE_TOOLS = [
  { value: "CodeInterpreter", label: "Code Interpreter", description: "Run Python code in sandbox" },
  { value: "FileSearch", label: "File Search", description: "RAG with semantic search" },
  { value: "AzureAISearch", label: "Azure AI Search", description: "Enterprise search system" },
  { value: "BingGrounding", label: "Bing Grounding", description: "Real-time web search" },
  { value: "FunctionCalling", label: "Function Calling", description: "Custom function integration" },
  { value: "AzureFunctions", label: "Azure Functions", description: "Serverless code execution" },
  { value: "OpenAPI", label: "OpenAPI", description: "Connect to REST APIs" },
  { value: "BrowserAutomation", label: "Browser Automation", description: "Automated browsing" },
] as const;

/**
 * Form validation schema
 */
const agentFormSchema = z.object({
  name: z.string().min(1, "Name is required").max(100, "Name must be less than 100 characters"),
  description: z.string().max(500, "Description must be less than 500 characters").optional(),
  instructions: z
    .string()
    .min(1, "Instructions are required")
    .max(10000, "Instructions must be less than 10000 characters"),
  model: z.string().min(1, "Model selection is required"),
  temperature: z
    .number()
    .min(0, "Temperature must be at least 0")
    .max(2, "Temperature must be at most 2")
    .optional(),
  maxTokens: z
    .number()
    .min(1, "Max tokens must be at least 1")
    .max(100000, "Max tokens must be at most 100000")
    .optional(),
  maxDurationSeconds: z
    .number()
    .min(1, "Max duration must be at least 1 second")
    .max(3600, "Max duration must be at most 3600 seconds")
    .optional(),
  tools: z.array(z.string()).optional(),
});

type AgentFormValues = z.infer<typeof agentFormSchema>;

interface AgentEditorFormProps {
  agent?: Agent;
  mode: "create" | "edit";
}

/**
 * Agent Editor Form Component
 *
 * Provides a comprehensive form for creating or editing agent definitions with:
 * - Basic information (name, description, instructions)
 * - Azure AI Foundry model selection
 * - Budget constraints (tokens, duration)
 * - Tool selection
 */
export function AgentEditorForm({ agent, mode }: AgentEditorFormProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedTools, setSelectedTools] = useState<string[]>(agent?.tools || []);

  const form = useForm<AgentFormValues>({
    resolver: zodResolver(agentFormSchema),
    defaultValues: {
      name: agent?.name || "",
      description: agent?.description || "",
      instructions: agent?.instructions || "",
      model: (agent?.modelProfile?.model as string) || "gpt-4",
      temperature: (agent?.modelProfile?.temperature as number) || 0.7,
      maxTokens: agent?.budget?.maxTokens || 4000,
      maxDurationSeconds: agent?.budget?.maxDurationSeconds || 60,
      tools: agent?.tools || [],
    },
  });

  const onSubmit = async (values: AgentFormValues) => {
    setIsSubmitting(true);
    setError(null);

    try {
      const requestData: CreateAgentRequest | UpdateAgentRequest = {
        name: values.name,
        description: values.description || undefined,
        instructions: values.instructions,
        modelProfile: {
          model: values.model,
          temperature: values.temperature || 0.7,
        },
        budget: {
          maxTokens: values.maxTokens,
          maxDurationSeconds: values.maxDurationSeconds,
        },
        tools: selectedTools.length > 0 ? selectedTools : undefined,
      };

      if (mode === "create") {
        await createAgent(requestData as CreateAgentRequest);
      } else if (agent) {
        await updateAgent(agent.agentId, requestData as UpdateAgentRequest);
      }

      // Navigate back to agents list on success
      router.push("/agents");
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save agent");
      console.error("Error saving agent:", err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const toggleTool = (toolValue: string) => {
    setSelectedTools((prev) =>
      prev.includes(toolValue) ? prev.filter((t) => t !== toolValue) : [...prev, toolValue]
    );
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        {/* Error Display */}
        {error && (
          <Card className="border-destructive">
            <CardHeader>
              <CardTitle className="text-destructive">Error</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm">{error}</p>
            </CardContent>
          </Card>
        )}

        {/* Basic Information */}
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
            <CardDescription>Configure the agent&apos;s identity and purpose</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Name *</FormLabel>
                  <FormControl>
                    <Input placeholder="Invoice Classifier" {...field} />
                  </FormControl>
                  <FormDescription>A descriptive name for your agent</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Description</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="Classifies incoming invoices and routes them appropriately"
                      {...field}
                    />
                  </FormControl>
                  <FormDescription>Optional description of what this agent does</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="instructions"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Instructions (System Prompt) *</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="You are an AI assistant that classifies invoices..."
                      className="min-h-[200px] font-mono text-sm"
                      {...field}
                    />
                  </FormControl>
                  <FormDescription>
                    System prompt that defines the agent&apos;s behavior and role
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/* Azure AI Foundry Model Configuration */}
        <Card>
          <CardHeader>
            <CardTitle>Azure AI Foundry Model Configuration</CardTitle>
            <CardDescription>Select the LLM model and configure its parameters</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="model"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Model *</FormLabel>
                  <Select onValueChange={field.onChange} defaultValue={field.value}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a model" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {AVAILABLE_MODELS.map((model) => (
                        <SelectItem key={model.value} value={model.value}>
                          {model.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>Azure OpenAI model to use for this agent</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="temperature"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Temperature</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      step="0.1"
                      min="0"
                      max="2"
                      {...field}
                      onChange={(e) => field.onChange(parseFloat(e.target.value) || undefined)}
                      value={field.value?.toString() || ""}
                    />
                  </FormControl>
                  <FormDescription>
                    Controls randomness. Lower is more focused, higher is more creative (0-2, default
                    0.7)
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/* Budget Constraints */}
        <Card>
          <CardHeader>
            <CardTitle>Budget Constraints</CardTitle>
            <CardDescription>Set limits on resource usage per agent run</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="maxTokens"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Max Tokens</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min="1"
                      max="100000"
                      {...field}
                      onChange={(e) => field.onChange(parseInt(e.target.value, 10) || undefined)}
                      value={field.value?.toString() || ""}
                    />
                  </FormControl>
                  <FormDescription>
                    Maximum tokens the agent can use per run (default 4000)
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="maxDurationSeconds"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Max Duration (seconds)</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min="1"
                      max="3600"
                      {...field}
                      onChange={(e) => field.onChange(parseInt(e.target.value, 10) || undefined)}
                      value={field.value?.toString() || ""}
                    />
                  </FormControl>
                  <FormDescription>
                    Maximum time the agent can run in seconds (default 60)
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/* Azure AI Foundry Tools */}
        <Card>
          <CardHeader>
            <CardTitle>Azure AI Foundry Tools</CardTitle>
            <CardDescription>
              Select built-in tools to enhance your agent&apos;s capabilities
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 sm:grid-cols-2">
              {AVAILABLE_TOOLS.map((tool) => (
                <div
                  key={tool.value}
                  className={`border-border cursor-pointer rounded-lg border p-4 transition-colors ${
                    selectedTools.includes(tool.value)
                      ? "border-primary bg-primary/5"
                      : "hover:bg-accent"
                  }`}
                  onClick={() => toggleTool(tool.value)}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <h4 className="font-medium">{tool.label}</h4>
                      <p className="text-muted-foreground mt-1 text-xs">{tool.description}</p>
                    </div>
                    <div
                      className={`ml-2 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-sm border ${
                        selectedTools.includes(tool.value)
                          ? "border-primary bg-primary text-primary-foreground"
                          : "border-input"
                      }`}
                    >
                      {selectedTools.includes(tool.value) && (
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="3"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className="h-3 w-3"
                        >
                          <polyline points="20 6 9 17 4 12" />
                        </svg>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
            {selectedTools.length > 0 && (
              <p className="text-muted-foreground mt-4 text-sm">
                {selectedTools.length} tool{selectedTools.length === 1 ? "" : "s"} selected
              </p>
            )}
          </CardContent>
        </Card>

        {/* Form Actions */}
        <div className="flex justify-end gap-4">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push("/agents")}
            disabled={isSubmitting}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving..." : mode === "create" ? "Create Agent" : "Update Agent"}
          </Button>
        </div>
      </form>
    </Form>
  );
}
