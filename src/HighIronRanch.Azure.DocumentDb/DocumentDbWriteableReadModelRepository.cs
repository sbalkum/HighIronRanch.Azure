using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighIronRanch.Core;
using HighIronRanch.Core.Helpers;
using HighIronRanch.Core.Repositories;
using HighIronRanch.Core.Services;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace HighIronRanch.Azure.DocumentDb
{
    public class DocumentDbWritableViewModelRepository : DocumentDbViewModelRepository, IWritableViewModelRepository
    {
        public DocumentDbWritableViewModelRepository(IDocumentDbSettings settings, IDocumentDbClientFactory clientFactory, ILogger logger)
            : base(settings, clientFactory, logger)
        {
            
        }

        /// <summary>
        /// Verifies the collection exists and creates it if it does not.
        /// Overrides base class implementation in order to add creation logic.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected override async Task CreateCollectionIfNecessaryAsync<T>()
        {
            var databaseLink = UriFactory.CreateDatabaseUri(_settings.DocumentDbRepositoryDatabaseId);
            var client = await _clientFactory.GetClientAsync(_settings);
            var collection = client.CreateDocumentCollectionQuery(databaseLink)
                                .Where(c => c.Id == typeof(T).Name)
                                .AsEnumerable()
                                .FirstOrDefault();
            if (collection == null)
            {
                _logger.Information(Common.LoggerContext, "Creating collection {0}", typeof (T).Name);
                await client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection() { Id = typeof(T).Name });
            }
        }

        public void Delete<T>(T item) where T : IViewModel
        {
            AsyncHelpers.RunSync(() => DeleteAsync(item));
        }

        public async Task DeleteAsync<T>(T item) where T : IViewModel
        {
            var documentLink = GetDocumentLink<T>(item.Id.ToString());
            var client = await _clientFactory.GetClientAsync(_settings);
            await client.DeleteDocumentAsync(documentLink);
        }

        public void Insert<T>(T item) where T : IViewModel
        {
            AsyncHelpers.RunSync(() => InsertAsync(item));
        }

        public async Task InsertAsync<T>(T item) where T : IViewModel
        {
            var collectionLink = await GetCollectionLinkAsync<T>();
            var client = await _clientFactory.GetClientAsync(_settings);
            await InsertAsync(client, collectionLink, item);
        }

        public void Insert<T>(IEnumerable<T> items) where T : IViewModel
        {
            AsyncHelpers.RunSync(() => InsertAsync(items));
        }

        public async Task UpdateAsync<T>(T item) where T : IViewModel
        {
            await SaveAsync(item);
        }

        public void Update<T>(T item) where T : IViewModel
        {
            AsyncHelpers.RunSync(() => UpdateAsync(item));
        }

        public async Task InsertAsync<T>(IEnumerable<T> items) where T : IViewModel
        {
            var collectionLink = await GetCollectionLinkAsync<T>();
            var client = await _clientFactory.GetClientAsync(_settings);
            foreach (var item in items)
            {
                await InsertAsync(client, collectionLink, item);
            }
        }

        protected async Task InsertAsync<T>(DocumentClient client, Uri collectionLink, T item) where T : IViewModel
        {
            var tryCount = 3;
            while (tryCount > 0)
            {
                tryCount--;
                try
                {
                    await client.CreateDocumentAsync(collectionLink, item);
                    return;
                }
                catch (DocumentClientException documentClientException)
                {
                    var statusCode = (int)documentClientException.StatusCode;
                    if (statusCode == 429)
                    {
                        _logger.Warning(Common.LoggerContext, "429 http code inserting {0}", item.Id);
                        Thread.Sleep(documentClientException.RetryAfter);
                    }
                    //add other error codes to trap here e.g. 503 - Service Unavailable
                    else
                    {
                        _logger.Error(Common.LoggerContext, "{0} http code inserting {1}", statusCode, item.Id);
                        throw;
                    }
                }
/*
                catch (AggregateException aggregateException) when (aggregateException is DocumentClientException)
                {
                    var statusCode = (int)aggregateException.StatusCode;
                    if (statusCode == 429)
                    {
                        Thread.Sleep(documentClientException.RetryAfter);
                    }
                    //add other error codes to trap here e.g. 503 - Service Unavailable
                    else
                    {
                        throw;
                    }
                }
*/
            }

            _logger.Error(Common.LoggerContext, "Maximum retries exceeded inserting {0}", item.Id);
            throw new Exception("Maximum retries exceeded");
        }

        public void Save<T>(T item) where T : IViewModel
        {
            AsyncHelpers.RunSync(() => SaveAsync(item));
        }

        public async Task SaveAsync<T>(T item) where T : IViewModel
        {
            var documentLink = GetDocumentLink<T>(item.Id.ToString());
            var client = await _clientFactory.GetClientAsync(_settings);
            await client.ReplaceDocumentAsync(documentLink, item);
        }

        public void Truncate<T>() where T : IViewModel
        {
            AsyncHelpers.RunSync(() => TruncateAsync<T>());
        }

        public async Task TruncateAsync<T>() where T : IViewModel
        {
            _logger.Information(Common.LoggerContext, "Deleting collection {0}", typeof(T).Name);

            var collectionLink = await GetCollectionLinkAsync<T>();
            var client = await _clientFactory.GetClientAsync(_settings);
            await client.DeleteDocumentCollectionAsync(collectionLink);
            _collectionUris.Remove(typeof (T));
        }

        public async Task DeleteDatabaseAsync()
        {
            _logger.Information(Common.LoggerContext, "Deleting database {0}", _settings.DocumentDbRepositoryDatabaseId);

            var client = await _clientFactory.GetClientAsync(_settings);
            await client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(_settings.DocumentDbRepositoryDatabaseId));
        }
    }
}