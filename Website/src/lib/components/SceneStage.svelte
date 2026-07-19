<script>
  import { ChevronLeft, ChevronRight } from "@lucide/svelte";

  let { scenes = [] } = $props();
  let activeIndex = $state(0);
  const active = $derived(scenes[activeIndex] ?? null);

  function step(direction) {
    if (scenes.length === 0) return;
    activeIndex = (activeIndex + direction + scenes.length) % scenes.length;
  }
</script>

<div class="scene-stage">
  {#if active}
    <div class="scene-stage-image" style:background-image={`url('${active.localPath}')`} role="img" aria-label={active.alt}></div>
    <div class="scene-stage-shade"></div>
    <div class="scene-stage-copy">
      <span>{active.meta}</span>
      <strong>{active.title}</strong>
      <small>{String(activeIndex + 1).padStart(2, "0")} / {String(scenes.length).padStart(2, "0")}</small>
    </div>
    <div class="scene-controls">
      <button type="button" aria-label="Vorherige Szene" onclick={() => step(-1)}><ChevronLeft size={18} /></button>
      <div class="scene-dots" aria-label="Szene auswaehlen">
        {#each scenes as scene, index (scene.id)}
          <button
            class:active={index === activeIndex}
            type="button"
            aria-label={scene.title}
            aria-pressed={index === activeIndex}
            onclick={() => (activeIndex = index)}
          ></button>
        {/each}
      </div>
      <button type="button" aria-label="Naechste Szene" onclick={() => step(1)}><ChevronRight size={18} /></button>
    </div>
  {:else}
    <div class="scene-stage-empty">Szenen werden geladen</div>
  {/if}
</div>
