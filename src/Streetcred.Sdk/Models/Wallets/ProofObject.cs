﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace Streetcred.Sdk.Models.Wallets
{
    /// <summary>
    /// Represents a proof stored in the wallet.
    /// </summary>
    public class ProofObject
    {
        /// <summary>
        /// Gets or sets the proof identifiers.
        /// </summary>
        /// <value>
        /// The proof identifiers.
        /// </value>
        [JsonProperty("identifiers")]
        public List<ProofIdentifier> Identifiers { get; set; }

        /// <summary>
        /// Gets or sets the requested proof.
        /// </summary>
        /// <value>
        /// The requested proof.
        /// </value>
        [JsonProperty("requested_proof")]
        public RequestedProof RequestedProof { get; set; }
    }

    /// <summary>
    /// Represents an individual proof identifier stored in a proof in the wallet.
    /// </summary>
    public class ProofIdentifier
    {
        /// <summary>
        /// Gets or sets the schema identifier.
        /// </summary>
        /// <value>
        /// The schema identifier.
        /// </value>
        [JsonProperty("schema_id")]
        public string SchemaId { get; set; }

        /// <summary>
        /// Gets or sets the credential definition identifier.
        /// </summary>
        /// <value>
        /// The credential definition identifier.
        /// </value>
        [JsonProperty("cred_def_id")]
        public string CredentialDefintionId { get; set; }

        /// <summary>
        /// Gets or sets the revocation registry identifier.
        /// </summary>
        /// <value>
        /// The revocation registry identifier.
        /// </value>
        [JsonProperty("rev_reg_id")]
        public string RevocationRegistryId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>
        /// The timestamp.
        /// </value>
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    public class RequestedProof
    {
        /// <summary>
        /// Gets or sets the revealed attributes.
        /// </summary>
        /// <value>
        /// The revealed attributes.
        /// </value>
        [JsonProperty("revealed_attrs")]
        public Dictionary<string, ProofAttribute> RevealedAttributes { get; set; }

        /// <summary>
        /// Gets or sets the revealed attributes.
        /// </summary>
        /// <value>
        /// The revealed attributes.
        /// </value>
        [JsonProperty("self_attested_attrs")]
        public Dictionary<string, ProofAttribute> SelfAttestedAttributes { get; set; }
    }

    public class ProofAttribute
    {
        /// <summary>
        /// Gets or sets the sub proof index.
        /// </summary>
        /// <value>
        /// The sub proof index.
        /// </value>
        [JsonProperty("sub_proof_index")]
        public int SubProofIndex { get; set; }

        /// <summary>
        /// Gets or sets the raw value of the attribute.
        /// </summary>
        /// <value>
        /// The raw value of the attribute.
        /// </value>
        [JsonProperty("raw")]
        public string Raw { get; set; }

        /// <summary>
        /// Gets or sets the encoded value of the attribute.
        /// </summary>
        /// <value>
        /// The encoded value of the attribute.
        /// </value>
        [JsonProperty("encoded")]
        public string Encoded { get; set; }
    }
}
