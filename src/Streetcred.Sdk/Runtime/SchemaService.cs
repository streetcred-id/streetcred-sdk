﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Streetcred.Sdk.Contracts;
using Streetcred.Sdk.Model.Records;
using Streetcred.Sdk.Utils;

namespace Streetcred.Sdk.Runtime
{
    public class SchemaService : ISchemaService
    {
        private readonly IWalletRecordService _recordService;
        private readonly ILedgerService _ledgerService;
        private readonly ITailsService _tailsService;

        public SchemaService(
            IWalletRecordService recordService,
            ILedgerService ledgerService,
            ITailsService tailsService)
        {
            _recordService = recordService;
            _ledgerService = ledgerService;
            _tailsService = tailsService;
        }

        /// <summary>
        /// Creates the schema asynchronous.
        /// </summary>
        /// <param name="pool">The pool.</param>
        /// <param name="wallet">The wallet.</param>
        /// <param name="issuerDid">The issuer did.</param>
        /// <param name="name">The name.</param>
        /// <param name="version">The version.</param>
        /// <param name="attributeNames">The attribute names.</param>
        /// <returns></returns>
        public async Task<string> CreateSchemaAsync(Pool pool, Wallet wallet, string issuerDid, string name,
            string version,
            string[] attributeNames)
        {
            var schema = await AnonCreds.IssuerCreateSchemaAsync(issuerDid, name, version, attributeNames.ToJson());

            var schemaRecord = new SchemaRecord {SchemaId = schema.SchemaId};

            await _ledgerService.RegisterSchemaAsync(pool, wallet, issuerDid, schema.SchemaJson);
            await _recordService.AddAsync(wallet, schemaRecord);

            return schema.SchemaId;
        }

        /// <summary>
        /// Creates the credential definition asynchronous.
        /// </summary>
        /// <param name="pool">The pool.</param>
        /// <param name="wallet">The wallet.</param>
        /// <param name="schemaId">The schema identifier.</param>
        /// <param name="issuerDid">The issuer did.</param>
        /// <param name="supportsRevocation">if set to <c>true</c> [supports revocation].</param>
        /// <returns></returns>
        public async Task<string> CreateCredentialDefinitionAsync(Pool pool, Wallet wallet, string schemaId,
            string issuerDid,
            bool supportsRevocation)
        {
            var definitionRecord = new DefinitionRecord();
            var schema = await _ledgerService.LookupSchemaAsync(pool, issuerDid, schemaId);

            var credentialDefinition = await AnonCreds.IssuerCreateAndStoreCredentialDefAsync(wallet, issuerDid,
                schema.ObjectJson, "Tag", null, new {supports_revocation = supportsRevocation}.ToJson());

            await _ledgerService.RegisterCredentialDefinitionAsync(wallet, pool, issuerDid,
                credentialDefinition.CredDefJson);

            definitionRecord.Revocable = supportsRevocation;
            definitionRecord.DefinitionId = credentialDefinition.CredDefId;

            if (supportsRevocation)
            {
                var storageId = Guid.NewGuid().ToString().ToLowerInvariant();
                var blobWriter = await _tailsService.GetBlobStorageWriterAsync(storageId);

                var revRegDefConfig = "{\"issuance_type\":\"ISSUANCE_ON_DEMAND\",\"max_cred_num\":1000}";
                var revocationRegistry = await AnonCreds.IssuerCreateAndStoreRevocRegAsync(wallet, issuerDid, null,
                    "Tag", credentialDefinition.CredDefId, revRegDefConfig, blobWriter);

                await _ledgerService.RegisterRevocationRegistryDefinitionAsync(wallet, pool, issuerDid,
                    revocationRegistry.RevRegEntryJson);
                await _ledgerService.SendRevocationRegistryEntryAsync(wallet, pool, issuerDid,
                    revocationRegistry.RevRegId, "CL_ACCUM", revocationRegistry.RevRegEntryJson);

                definitionRecord.RevocationRegistryId = revocationRegistry.RevRegId;
                definitionRecord.TailsStorageId = storageId;
            }

            await _recordService.AddAsync(wallet, definitionRecord);

            return credentialDefinition.CredDefId;
        }

        /// <summary>
        /// Gets the schemas asynchronous.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<List<SchemaRecord>> ListSchemasAsync(Wallet wallet) =>
            _recordService.SearchAsync<SchemaRecord>(wallet, null, null);

        /// <summary>
        /// Gets the credential definitions asynchronous.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<List<DefinitionRecord>> ListCredentialDefinitionsAsync(Wallet wallet) =>
            _recordService.SearchAsync<DefinitionRecord>(wallet, null, null);

        /// <summary>
        /// Gets the credential definition asynchronous.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="credentialDefinitionId">The credential definition identifier.</param>
        /// <returns></returns>
        public Task<DefinitionRecord> GetCredentialDefinitionAsync(Wallet wallet, string credentialDefinitionId) =>
            _recordService.GetAsync<DefinitionRecord>(wallet, credentialDefinitionId);
    }
}