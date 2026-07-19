<script>
  import { CircleCheck, CircleDashed, CircleDotDashed, TriangleAlert } from "@lucide/svelte";
  import { statusLabels } from "../site-data.js";

  let { status = "partial", compact = false } = $props();
  const normalized = $derived(statusLabels[status] ? status : "partial");
</script>

<span class:compact class="status-badge status-{normalized}">
  {#if normalized === "verified"}
    <CircleCheck size={compact ? 12 : 14} />
  {:else if normalized === "blocked"}
    <TriangleAlert size={compact ? 12 : 14} />
  {:else if normalized === "planned"}
    <CircleDashed size={compact ? 12 : 14} />
  {:else}
    <CircleDotDashed size={compact ? 12 : 14} />
  {/if}
  {statusLabels[normalized]}
</span>
