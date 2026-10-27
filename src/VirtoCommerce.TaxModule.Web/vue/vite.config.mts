import path from "node:path";
import { getDynamicModuleConfiguration } from "@vc-shell/mf-module";

// Tax providers of stores: a Module Federation remote of the Admin Shell host.
// Output: <module>/plugins/admin-shell/ (remoteEntry.js + public/plugin.json),
// served by the platform at /modules/$(VirtoCommerce.Tax)/plugins/admin-shell/.
const base = "/modules/$(VirtoCommerce.Tax)/plugins/admin-shell/";

export default getDynamicModuleConfiguration({
  appId: "admin-shell",
  moduleRoot: path.resolve(__dirname, ".."),
  entry: "./src/modules/index.ts",
  remoteName: "tax_admin",
  base,
});
