<script>
  let { compact = false, docs = false } = $props();
</script>

<a class:compact class="brand" href="./index.html" aria-label="YjsE Startseite">
  <span class="brand-mark" aria-hidden="true">
    <i></i><i></i><i></i><i></i><i></i>
  </span>
  <span class="brand-copy">
    <strong>YjsE</strong>
    {#if !compact}<small>{docs ? "Engine Docs" : "YoungJibbit's Engine"}</small>{/if}
  </span>
</a>
