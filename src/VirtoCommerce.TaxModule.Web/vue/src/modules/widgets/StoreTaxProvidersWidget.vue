<template>
  <VcWidget
    icon="lucide-percent"
    :title="$t('TAX.TITLE')"
    :disabled="!storeId"
    @click="open"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { injectBladeContext, useBlade } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";

// the store shared by the Store module's blade via defineBladeContext({ item })
const ctx = injectBladeContext();
const storeId = computed(() => (ctx.value.item as { id?: string } | undefined)?.id);
const { openBlade } = useBlade();

const open = () => openBlade({ name: "TaxProviders", param: storeId.value });
</script>
