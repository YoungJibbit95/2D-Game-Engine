<script>
  import { BookOpen, Download, GitBranch, Menu, X } from "@lucide/svelte";
  import Brand from "./Brand.svelte";

  let { docs = false } = $props();
  let menuOpen = $state(false);
  let scrolled = $state(false);

  function closeMenu() {
    menuOpen = false;
  }
</script>

<svelte:window onscroll={() => (scrolled = window.scrollY > 16)} onkeydown={(event) => event.key === "Escape" && closeMenu()} />

<header class:scrolled class="site-header">
  <Brand {docs} />

  <nav class:open={menuOpen} class="site-nav" aria-label="Hauptnavigation">
    <a class:active={!docs} href="./index.html" onclick={closeMenu}>Game</a>
    <a href="./index.html#engine" onclick={closeMenu}>Engine</a>
    <a href="./index.html#download" onclick={closeMenu}>Download</a>
    <a class:active={docs} href="./docs.html" onclick={closeMenu}>Docs</a>
  </nav>

  <div class="header-actions">
    <a class="header-github icon-action" href="https://github.com/YoungJibbit95/2D-Game-Engine" aria-label="YjsE auf GitHub">
      <GitBranch size={18} strokeWidth={1.8} />
    </a>
    {#if docs}
      <a class="header-cta" href="./index.html#download"><Download size={16} /> Build status</a>
    {:else}
      <a class="header-cta" href="./docs.html"><BookOpen size={16} /> Engine docs</a>
    {/if}
    <button
      class="menu-toggle icon-action"
      type="button"
      aria-label={menuOpen ? "Navigation schliessen" : "Navigation oeffnen"}
      aria-expanded={menuOpen}
      onclick={() => (menuOpen = !menuOpen)}
    >
      {#if menuOpen}<X size={20} />{:else}<Menu size={20} />{/if}
    </button>
  </div>
</header>
