﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.BlobStorageApi;
using Hyperledger.Indy.DidApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Streetcred.Sdk.Contracts;
using Streetcred.Sdk.Model;
using Streetcred.Sdk.Model.Credentials;
using Streetcred.Sdk.Model.Records;
using Streetcred.Sdk.Model.Records.Search;
using Streetcred.Sdk.Utils;

namespace Streetcred.Sdk.Runtime
{
    public class CredentialService : ICredentialService
    {
        private readonly IRouterService _routerService;
        private readonly ILedgerService _ledgerService;
        private readonly IConnectionService _connectionService;
        private readonly IWalletRecordService _recordService;
        private readonly IMessageSerializer _messageSerializer;
        private readonly ISchemaService _schemaService;
        private readonly ITailsService _tailsService;
        private readonly IProvisioningService _provisioningService;
        private readonly ILogger<CredentialService> _logger;

        public CredentialService(
            IRouterService routerService,
            ILedgerService ledgerService,
            IConnectionService connectionService,
            IWalletRecordService recordService,
            IMessageSerializer messageSerializer,
            ISchemaService schemaService,
            ITailsService tailsService,
            IProvisioningService provisioningService,
            ILogger<CredentialService> logger)
        {
            _routerService = routerService;
            _ledgerService = ledgerService;
            _connectionService = connectionService;
            _recordService = recordService;
            _messageSerializer = messageSerializer;
            _schemaService = schemaService;
            _tailsService = tailsService;
            _provisioningService = provisioningService;
            _logger = logger;
        }


        /// <inheritdoc />
        public Task<CredentialRecord> GetAsync(Wallet wallet, string credentialId) =>
            _recordService.GetAsync<CredentialRecord>(wallet, credentialId);

        /// <inheritdoc />
        public Task<List<CredentialRecord>> ListAsync(Wallet wallet, SearchRecordQuery query = null, int count = 100) =>
            _recordService.SearchAsync<CredentialRecord>(wallet, query, null, count);

        /// <inheritdoc />
        public async Task<string> StoreOfferAsync(Wallet wallet, CredentialOffer credentialOffer,
                string connectionId)
            // TODO: Remove 'connectionId' parameter and resolve the connection
            // from the @type which should include DID details
        {
            var connection = await _connectionService.GetAsync(wallet, connectionId);
            var (offerDetails, _) = await _messageSerializer.UnpackSealedAsync<CredentialOfferDetails>(
                credentialOffer.Content,
                wallet, await Did.KeyForLocalDidAsync(wallet, connection.MyDid));
            var offerJson = offerDetails.OfferJson;

            var offer = JObject.Parse(offerJson);
            var definitionId = offer["cred_def_id"].ToObject<string>();
            var schemaId = offer["schema_id"].ToObject<string>();
            var nonce = offer["nonce"].ToObject<string>();

            // Write offer record to local wallet
            var credentialRecord = new CredentialRecord
            {
                Id = Guid.NewGuid().ToString(),
                OfferJson = offerJson,
                ConnectionId = connection.GetId(),
                CredentialDefinitionId = definitionId,
                State = CredentialState.Offered
            };
            credentialRecord.Tags.Add("connectionId", connection.GetId());
            credentialRecord.Tags.Add("nonce", nonce);
            credentialRecord.Tags.Add("schemaId", schemaId);
            credentialRecord.Tags.Add("definitionId", definitionId);

            await _recordService.AddAsync(wallet, credentialRecord);

            return credentialRecord.GetId();
        }

        /// <inheritdoc />
        public async Task AcceptOfferAsync(Wallet wallet, Pool pool, string credentialId,
            Dictionary<string, string> values)
        {
            var credential = await _recordService.GetAsync<CredentialRecord>(wallet, credentialId);
            var connection = await _connectionService.GetAsync(wallet, credential.ConnectionId);
            var definition =
                await _ledgerService.LookupDefinitionAsync(pool, connection.MyDid, credential.CredentialDefinitionId);
            var provisioning = await _provisioningService.GetProvisioningAsync(wallet);

            var request = await AnonCreds.ProverCreateCredentialReqAsync(wallet, connection.MyDid, credential.OfferJson,
                definition.ObjectJson, provisioning.MasterSecretId);

            // Update local credential record with new info and advance the state
            credential.CredentialRequestMetadataJson = request.CredentialRequestMetadataJson;
            await credential.TriggerAsync(CredentialTrigger.Request);
            await _recordService.UpdateAsync(wallet, credential);

            var details = new CredentialRequestDetails
            {
                OfferJson = credential.OfferJson,
                CredentialRequestJson = request.CredentialRequestJson,
                CredentialValuesJson = CredentialUtils.FormatCredentialValues(values)
            };

            var requestMessage =
                await _messageSerializer.PackSealedAsync<CredentialRequest>(details, wallet, connection.MyVk,
                    connection.TheirVk);

            await _routerService.ForwardAsync(new ForwardEnvelopeMessage
            {
                Content = requestMessage.ToJson(),
                To = connection.TheirDid
            }, connection.Endpoint);
        }

        /// <inheritdoc />
        public async Task StoreCredentialAsync(Pool pool, Wallet wallet, Credential credential, string connectionId)
            // TODO: Remove 'connectionId' parameter and resolve the connection
            // from the @type which should include DID details
        {
            var connection = await _connectionService.GetAsync(wallet, connectionId);
            var (details, _) = await _messageSerializer.UnpackSealedAsync<CredentialDetails>(credential.Content,
                wallet, await Did.KeyForLocalDidAsync(wallet, connection.MyDid));

            var offer = JObject.Parse(details.CredentialJson);
            var definitionId = offer["cred_def_id"].ToObject<string>();
            var schemaId = offer["schema_id"].ToObject<string>();
            var revRegId = offer["rev_reg_id"]?.ToObject<string>();

            var credentialSearch =
                await _recordService.SearchAsync<CredentialRecord>(wallet, new SearchRecordQuery
                {
                    {"schemaId", schemaId},
                    {"definitionId", definitionId},
                    {"connectionId", connectionId}
                }, null, 1);

            var credentialRecord = credentialSearch.Single();
            // TODO: Should throw or resolve conflict gracefully if multiple credential records are found

            var credentialDefinition = await _ledgerService.LookupDefinitionAsync(pool, connection.MyDid, definitionId);

            string revocationRegistryDefinitionJson = null;
            if (!string.IsNullOrEmpty(revRegId))
            {
                // If credential supports revocation, lookup registry definition
                var revocationRegistry =
                    await _ledgerService.LookupRevocationRegistryDefinitionAsync(pool, connection.MyDid, revRegId);
                revocationRegistryDefinitionJson = revocationRegistry.ObjectJson;
            }

            var credentialId = await AnonCreds.ProverStoreCredentialAsync(wallet, null,
                credentialRecord.CredentialRequestMetadataJson,
                details.CredentialJson, credentialDefinition.ObjectJson, revocationRegistryDefinitionJson);

            credentialRecord.CredentialId = credentialId;
            await credentialRecord.TriggerAsync(CredentialTrigger.Issue);
            await _recordService.UpdateAsync(wallet, credentialRecord);
        }


        /// <inheritdoc />
        public async Task<CredentialOffer> CreateOfferAsync(string credentialDefinitionId, string connectionId,
            Wallet wallet, string issuerDid)
        {
            _logger.LogInformation(LoggingEvents.CreateOffer, "DefinitionId {0}, ConnectionId {1}, IssuerDid {2}",
                credentialDefinitionId, connectionId, issuerDid);

            var connection = await _connectionService.GetAsync(wallet, connectionId);
            var offerJson = await AnonCreds.IssuerCreateCredentialOfferAsync(wallet, credentialDefinitionId);
            var nonce = JObject.Parse(offerJson)["nonce"].ToObject<string>();

            // Write offer record to local wallet
            var credentialRecord = new CredentialRecord
            {
                Id = Guid.NewGuid().ToString(),
                CredentialDefinitionId = credentialDefinitionId,
                OfferJson = offerJson,
                State = CredentialState.Offered,
                ConnectionId = connection.GetId(),
            };
            credentialRecord.Tags.Add("nonce", nonce);
            credentialRecord.Tags.Add("connectionId", connection.GetId());

            await _recordService.AddAsync(wallet, credentialRecord);

            var credentialOffer = await _messageSerializer.PackSealedAsync<CredentialOffer>(
                new CredentialOfferDetails {OfferJson = offerJson},
                wallet,
                connection.MyVk,
                connection.TheirVk);
            return credentialOffer;
        }

        /// <inheritdoc />
        public async Task SendOfferAsync(string credentialDefinitionId, string connectionId, Wallet wallet,
            string issuerDid)
        {
            _logger.LogInformation(LoggingEvents.SendOffer, "DefinitionId {0}, ConnectionId {1}, IssuerDid {2}",
                credentialDefinitionId, connectionId, issuerDid);

            var connection = await _connectionService.GetAsync(wallet, connectionId);
            var offer = await CreateOfferAsync(credentialDefinitionId, connectionId, wallet, issuerDid);

            await _routerService.ForwardAsync(new ForwardEnvelopeMessage
            {
                Content = offer.ToJson(),
                To = connection.TheirDid
            }, connection.Endpoint);
        }

        /// <inheritdoc />
        public async Task<string> StoreCredentialRequestAsync(Wallet wallet, CredentialRequest credentialRequest,
                string connectionId)
            // TODO: Remove 'connectionId' parameter and resolve the connection
            // from the @type which should include DID details
        {
            _logger.LogInformation(LoggingEvents.StoreCredentialRequest, "ConnectionId {0},", connectionId);

            var connection = await _connectionService.GetAsync(wallet, connectionId);
            var (details, _) = await _messageSerializer.UnpackSealedAsync<CredentialRequestDetails>(
                credentialRequest.Content, wallet,
                await Did.KeyForLocalDidAsync(wallet, connection.MyDid));

            var request = JObject.Parse(details.OfferJson);
            var nonce = request["nonce"].ToObject<string>();

            var query = new SearchRecordQuery {{"nonce", nonce}};
            var credentialSearch = await _recordService.SearchAsync<CredentialRecord>(wallet, query, null, 1);

            var credential = credentialSearch.Single();
            // Offer should already be present
            // credential.OfferJson = details.OfferJson; 
            credential.ValuesJson = details.CredentialValuesJson;
            credential.RequestJson = details.CredentialRequestJson;

            await credential.TriggerAsync(CredentialTrigger.Request);

            await _recordService.UpdateAsync(wallet, credential);
            return credential.GetId();
        }

        /// <inheritdoc />
        public async Task IssueCredentialAsync(Pool pool, Wallet wallet, string issuerDid, string credentialId)
        {
            var credentialRecord = await _recordService.GetAsync<CredentialRecord>(wallet, credentialId);
            var definitionRecord =
                await _schemaService.GetCredentialDefinitionAsync(wallet, credentialRecord.CredentialDefinitionId);

            var connection = await _connectionService.GetAsync(wallet, credentialRecord.ConnectionId);

            if (credentialRecord.State != CredentialState.Requested)
                throw new Exception(
                    $"Credential sate was invalid. Expected '{CredentialState.Requested}', found '{credentialRecord.State}'");

            string revocationRegistryId = null;
            BlobStorageReader tailsReader = null;
            if (definitionRecord.Revocable)
            {
                revocationRegistryId = definitionRecord.RevocationRegistryId;
                tailsReader = await _tailsService.GetBlobStorageReaderAsync(definitionRecord.TailsStorageId);
            }

            var issuedCredential = await AnonCreds.IssuerCreateCredentialAsync(wallet, credentialRecord.OfferJson,
                credentialRecord.RequestJson, credentialRecord.ValuesJson, revocationRegistryId, tailsReader);

            if (definitionRecord.Revocable)
            {
                await _ledgerService.SendRevocationRegistryEntryAsync(wallet, pool, issuerDid,
                    definitionRecord.RevocationRegistryId,
                    "CL_ACCUM", issuedCredential.RevocRegDeltaJson);
                credentialRecord.CredentialRevocationId = issuedCredential.RevocId;
            }

            var credentialDetails = new CredentialDetails
            {
                CredentialJson = issuedCredential.CredentialJson,
                RevocationRegistryId = revocationRegistryId
            };

            await credentialRecord.TriggerAsync(CredentialTrigger.Issue);
            await _recordService.UpdateAsync(wallet, credentialRecord);

            var credential = await _messageSerializer.PackSealedAsync<Credential>(credentialDetails, wallet,
                connection.MyVk,
                connection.TheirVk);

            await _routerService.ForwardAsync(new ForwardEnvelopeMessage
            {
                Content = credential.ToJson(),
                To = connection.TheirDid
            }, connection.Endpoint);
        }

        /// <inheritdoc />
        public async Task RevokeCredentialAsync(Pool pool, Wallet wallet, string credentialId, string issuerDid)
        {
            var credential = await GetAsync(wallet, credentialId);
            var definition =
                await _schemaService.GetCredentialDefinitionAsync(wallet, credential.CredentialDefinitionId);

            // Check if the state machine is valid for revocation
            await credential.TriggerAsync(CredentialTrigger.Revoke);

            // Revoke the credential
            var tailsReader = await _tailsService.GetBlobStorageReaderAsync(definition.TailsStorageId);
            var revocRegistryDeltaJson = await AnonCreds.IssuerRevokeCredentialAsync(wallet, tailsReader,
                definition.RevocationRegistryId,
                credential.CredentialRevocationId);

            // Write the delta state on the ledger for the corresponding revocation registry
            await _ledgerService.SendRevocationRegistryEntryAsync(wallet, pool, issuerDid,
                definition.RevocationRegistryId,
                "CL_ACCUM", revocRegistryDeltaJson);

            // Update local credential record
            await _recordService.UpdateAsync(wallet, credential);
        }
    }
}