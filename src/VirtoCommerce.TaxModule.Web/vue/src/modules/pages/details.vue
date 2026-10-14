<template>
  <VcBlade
    :title="name"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tax-form tw-space-y-4">
        <VcCard :header="$t('TAX.GENERAL')">
          <div class="tax-card">
            <VcRow class="tw-gap-4">
              <VcCol>
                <VcField
                  :label="$t('TAX.FIELDS.NAME')"
                  :model-value="name"
                />
              </VcCol>
              <VcCol>
                <VcField
                  :label="$t('TAX.FIELDS.CODE')"
                  :model-value="item.code"
                  copyable
                />
              </VcCol>
            </VcRow>
            <VcRow class="tw-gap-4">
              <VcCol>
                <VcSwitch
                  v-model="item.isActive"
                  :label="$t('TAX.FIELDS.IS_ACTIVE')"
                  :hint="$t('TAX.FIELDS.IS_ACTIVE_HINT')"
                />
              </VcCol>
              <VcCol>
                <VcInput
                  :model-value="item.priority ?? 0"
                  :label="$t('TAX.FIELDS.PRIORITY')"
                  type="number"
                  @update:model-value="(v) => (item.priority = Number(v) || 0)"
                />
              </VcCol>
            </VcRow>
            <ImageField
              v-model="item.logoUrl"
              :label="$t('TAX.FIELDS.LOGO')"
              folder="/tax-providers"
            />
          </div>
        </VcCard>

        <SettingsEditor
          v-if="item.settings?.length"
          :settings="item.settings"
        />
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { defineBladeContext, useAsync, useBlade, useBladeForm, useLoading, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade, VcCard, VcCol, VcContainer, VcField, VcForm, VcInput, VcRow, VcSwitch } from "@vc-shell/framework/ui";
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { api } from "../api";
import ImageField from "../components/ImageField.vue";
import SettingsEditor from "../components/SettingsEditor.vue";
import { URL, providerName, type TaxProvider } from "../methods";
import { loadLegacyTexts } from "../utils";

// not routable: the provider and its store come only through options, which a page reload doesn't restore
defineBlade({
  url: "/tax-provider",
  name: "TaxProviderDetails",
  routable: false,
});

const { t } = useI18n({ useScope: "global" });
const { options, callParent } = useBlade<{ method?: TaxProvider; storeId: string }>();

const item = ref<TaxProvider>(JSON.parse(JSON.stringify(options.value?.method ?? {})));
const texts = ref<Record<string, unknown>>({});
const name = computed(() => providerName(texts.value, item.value));

const form = useBladeForm({
  data: item,
  closeConfirmMessage: computed(() => t("TAX.ALERTS.CLOSE_CONFIRMATION")),
});

const { loading: fetching, action: load } = useAsync(async () => {
  // a transient provider (never saved for this store) has no id: it is created by the first save
  if (item.value.id) item.value = await api.get<TaxProvider>(`${URL}/${item.value.id}`);
  form.setBaseline();
});

const { loading: saving, action: save } = useAsync(async () => {
  const saved = await api.put<TaxProvider>(URL, { ...item.value, storeId: options.value?.storeId });
  item.value.id = saved?.id ?? item.value.id;
  await load();
  await callParent("reload");
});

const loading = useLoading(fetching, saving);

// the provider being edited (with its unsaved settings), for widgets other modules add to this blade
// (registerExternalWidget with targetBlades: ["TaxProviderDetails"], e.g. Avalara's "Test connection")
defineBladeContext({ item });

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "save",
    title: t("TAX.TOOLBAR.SAVE"),
    icon: "lucide-save",
    permissions: "store:update",
    disabled: computed(() => !form.canSave.value),
    clickHandler: () => save(),
  },
  {
    id: "reset",
    title: t("TAX.TOOLBAR.RESET"),
    icon: "lucide-undo-2",
    disabled: computed(() => !form.isModified.value),
    clickHandler: () => form.revert(),
  },
]);

onMounted(async () => {
  texts.value = await loadLegacyTexts();
  await load();
});
</script>

<style lang="scss">
.tax-form {
  padding: 20px 22px 36px;
}

.tax-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}
</style>
