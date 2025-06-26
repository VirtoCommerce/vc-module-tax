# Virto Commerce Tax Module

[![CI status](https://github.com/VirtoCommerce/vc-module-tax/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-tax/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-tax&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-tax) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-tax&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-tax) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-tax&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-tax) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-tax&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-tax)

The Tax module provides a flexible way to evaluate taxes by using different tax providers and core abstractions for custom tax providers. It allows easy addition of custom rules for tax calculation.

The module includes FixedRateTaxProvider as a built-in tax provider.

The module provides an API to work with the tax provider list and allows you to connect the tax providers to a selected store. The list of available tax providers can be viewed and edited on the UI.

## Key features

* **Flexible tax calculation**: Evaluate tax using different tax providers, including the built-in FixedRateTaxProvider, and easily add custom rules for tax calculation.
* **UI integration**: Display the list of available tax providers on the user interface and edit their settings directly from the platform.
* **Store-level tax configuration**: Connect tax providers to specific stores and configure different tax rates based on location, product type, or other parameters.
* **Customizable tax providers**: Implement custom tax providers by creating new modules that inherit from the abstract TaxProviderBase class.
* **Programmatic tax provider registration**: Register new tax providers programmatically by implementing the ITaxProvider interface.
* **API access**: Access the list of available tax providers and their settings via the public API.

## Default providers

1. **FixedRateTaxProvider** is a built-in tax provider included in the Virto Commerce Tax Module. It calculates taxes based on fixed rates.
1. [Avalara.Tax](https://github.com/VirtoCommerce/vc-module-avatax)  real time integration with Avalara Tax automation. This module is officially certified by Avalara to be compatible with Avalara API.

## Documentation

* [Tax module user documentation](https://docs.virtocommerce.org/platform/user-guide/tax/overview/)
* [New tax provider registration](https://docs.virtocommerce.org/platform/developer-guide/Fundamentals/Taxes/new-tax-provider-registration/)
* [REST API](https://virtostart-demo-admin.govirto.com/docs/index.html?urls.primaryName=VirtoCommerce.Tax)
* [Tax configurations](https://docs.virtocommerce.org/platform/developer-guide/Configuration-Reference/appsettingsjson/#tax)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-tax)

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-tax/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

This software is licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at http://virtocommerce.com/opensourcelicense.

Unless required by the applicable law or agreed to in written form, the software
distributed under the License is provided on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
