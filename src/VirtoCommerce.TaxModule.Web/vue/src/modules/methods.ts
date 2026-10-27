import type { SettingEntry } from "./components/SettingsEditor.vue";
import { legacyText, type LegacyTexts } from "./utils";

// Tax providers of a store (api/taxes). The server picks the concrete type from typeName: PUT the whole object back.
export interface TaxProvider {
  id?: string | null;
  code?: string;
  typeName?: string;
  logoUrl?: string | null;
  isActive?: boolean;
  priority?: number;
  storeId?: string;
  settings?: SettingEntry[];
}

export const URL = "/api/taxes";

// a tax provider has no name of its own: the AngularJS texts per type ("tax.labels.<typeName>.name")
export const providerName = (texts: LegacyTexts, p: TaxProvider) =>
  legacyText(texts, "tax", "labels", p.typeName ?? "", "name") ?? p.typeName ?? p.code ?? "";
export const providerDescription = (texts: LegacyTexts, p: TaxProvider) =>
  legacyText(texts, "tax", "labels", p.typeName ?? "", "description") ?? "";
