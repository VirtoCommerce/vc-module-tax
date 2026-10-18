<template>
  <VcBlade
    :title="$t('TAX.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcDataTable
      :items="rows"
      :state-key="'TAX_PROVIDERS'"
      :active-item-id="activeItemId"
      :show-all-columns="false"
      :empty-state="{ icon: 'lucide-percent', title: $t('TAX.EMPTY') }"
      @row-click="(e: { data: Row }) => openDetails(e.data)"
    >
      <VcColumn
        id="logoUrl"
        :title="$t('TAX.FIELDS.LOGO')"
        type="image"
        width="72px"
        always-visible
      />
      <VcColumn
        id="displayName"
        :title="$t('TAX.FIELDS.NAME')"
        always-visible
      >
        <template #body="{ data: m }">
          <div class="tax-providers__name">{{ m.displayName }}</div>
          <div
            v-if="m.displayDescription"
            class="tax-providers__description"
          >
            {{ m.displayDescription }}
          </div>
        </template>
      </VcColumn>
      <VcColumn
        id="code"
        :title="$t('TAX.FIELDS.CODE')"
      />
      <VcColumn
        id="priority"
        :title="$t('TAX.FIELDS.PRIORITY')"
        width="90px"
      />
      <VcColumn
        id="isActive"
        :title="$t('TAX.FIELDS.STATUS')"
        always-visible
      >
        <template #body="{ data: m }">
          <div class="tw-inline-flex">
            <VcStatus :variant="m.isActive ? 'success' : 'info'">
              {{ $t(m.isActive ? "TAX.ACTIVE" : "TAX.INACTIVE") }}
            </VcStatus>
          </div>
        </template>
      </VcColumn>
    </VcDataTable>
  </VcBlade>
</template>

<script setup lang="ts">
import { useAsync, useBlade, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade, VcColumn, VcDataTable, VcStatus } from "@vc-shell/framework/ui";
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { api } from "../api";
import { URL, providerDescription, providerName, type TaxProvider } from "../methods";
import { useLegacyTexts } from "../utils";

// The tax providers of a store (param = store id)
defineBlade({
  url: "/tax-providers",
  name: "TaxProviders",
});

type Row = TaxProvider & { id: string; displayName: string; displayDescription: string };

const { t } = useI18n({ useScope: "global" });
const { param, openBlade, callParent, exposeToChildren } = useBlade();

const methods = ref<TaxProvider[]>([]);
const texts = useLegacyTexts();
const activeItemId = ref<string>();

// transient methods (registered, never saved for this store) have no id yet: the code identifies them
const rows = computed<Row[]>(() =>
  methods.value
    .map((m) => ({
      ...m,
      id: (m.id ?? m.code)!,
      displayName: providerName(texts.value, m),
      displayDescription: providerDescription(texts.value, m),
    }))
    .sort((a, b) => (a.priority ?? 0) - (b.priority ?? 0) || a.displayName.localeCompare(b.displayName)),
);

const { loading, action: load } = useAsync(async () => {
  const result = await api.post<{ results?: TaxProvider[] }>(`${URL}/search`, { storeIds: [param.value], take: 100 });
  methods.value = result.results ?? [];
});

const bladeToolbar = ref<IBladeToolbar[]>([
  { id: "refresh", title: t("TAX.TOOLBAR.REFRESH"), icon: "lucide-refresh-cw", clickHandler: () => load() },
]);

function openDetails(row: Row) {
  activeItemId.value = row.id;
  openBlade({
    name: "TaxProviderDetails",
    param: row.id,
    options: { method: methods.value.find((m) => (m.id ?? m.code) === row.id), storeId: param.value },
    onClose: () => (activeItemId.value = undefined),
  });
}

onMounted(load);

exposeToChildren({
  async reload() {
    await load();
    await callParent("reloadWidgets").catch(() => undefined); // the store blade's widget badges
  },
});
</script>

<style lang="scss">
.tax-providers__name {
  color: var(--neutrals-900);
}
.tax-providers__description {
  color: var(--neutrals-500);
  font-size: 12px;
}
</style>
