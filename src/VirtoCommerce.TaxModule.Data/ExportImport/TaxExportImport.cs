using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Data.ExportImport;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.TaxModule.Data.ExportImport
{
    public class TaxExportImport
    {
        private readonly ICrudService<TaxProvider> _taxProviderService;
        private readonly ISearchService<TaxProviderSearchCriteria, TaxProviderSearchResult, TaxProvider> _taxProviderSearchService;
        private readonly JsonSerializer _jsonSerializer;
        private readonly int _batchSize = 50;

        public TaxExportImport(ITaxProviderService taxProviderService, ITaxProviderSearchService taxProviderSearchService, JsonSerializer jsonSerializer)
        {
            _taxProviderService = (ICrudService<TaxProvider>)taxProviderService;
            _jsonSerializer = jsonSerializer;
            _taxProviderSearchService = (ISearchService<TaxProviderSearchCriteria, TaxProviderSearchResult, TaxProvider>)taxProviderSearchService;
        }

        public async Task DoExportAsync(Stream outStream, Action<ExportImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var progressInfo = new ExportImportProgressInfo { Description = "Tax providers are loading" };
            progressCallback(progressInfo);

            using (var sw = new StreamWriter(outStream))
            using (var writer = new JsonTextWriter(sw))
            {
                await writer.WriteStartObjectAsync();

                progressInfo.Description = "Tax providers are started to export";
                progressCallback(progressInfo);

                await writer.WritePropertyNameAsync("TaxProviders");
                await writer.SerializeJsonArrayWithPagingAsync(_jsonSerializer, _batchSize, async (skip, take) =>
                {
                    var searchCriteria = AbstractTypeFactory<TaxProviderSearchCriteria>.TryCreateInstance();
                    searchCriteria.Take = take;
                    searchCriteria.Skip = skip;
                    searchCriteria.WithoutTransient = true;

                    var searchResult = await _taxProviderSearchService.SearchAsync(searchCriteria);
                    return (GenericSearchResult<TaxProvider>)searchResult;
                }, (processedCount, totalCount) =>
                {
                    progressInfo.Description = $"{processedCount} of {totalCount} tax providers have been exported";
                    progressCallback(progressInfo);
                }, cancellationToken);

                await writer.WriteEndObjectAsync();
                await writer.FlushAsync();
            }
        }

        public async Task DoImportAsync(Stream inputStream, Action<ExportImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var progressInfo = new ExportImportProgressInfo();

            using (var streamReader = new StreamReader(inputStream))
            using (var reader = new JsonTextReader(streamReader))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName)
                    {
                        if (reader.Value.ToString() == "TaxProviders")
                        {
                            await reader.DeserializeJsonArrayWithPagingAsync<TaxProvider>(_jsonSerializer, _batchSize, items => _taxProviderService.SaveChangesAsync(items.ToArray()), processedCount =>
                            {
                                progressInfo.Description = $"{ processedCount } tax providers have been imported";
                                progressCallback(progressInfo);
                            }, cancellationToken);
                        }
                    }
                }
            }
        }
    }
}
