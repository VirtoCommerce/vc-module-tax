import * as blades from "./pages";
import * as locales from "./locales";
import { defineAppModule, registerExternalWidget } from "@vc-shell/framework";
import { markRaw } from "vue";
import StoreTaxProvidersWidget from "./widgets/StoreTaxProvidersWidget.vue";

// The AngularJS module's tax providers widget on the store blade ("storeDetail")
registerExternalWidget({
  id: "TaxStoreProviders",
  title: "Tax providers",
  component: markRaw(StoreTaxProvidersWidget),
  targetBlades: ["StoreDetails"],
  // not on a store that isn't saved yet
  isVisible: (blade) => !!blade?.param,
});

export default defineAppModule({
  blades,
  locales,
});
