﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace Streetcred.Sdk.Models.Wallets
{
    /// <summary>
    /// Represents a credential stored in the wallet.
    /// </summary>
    public struct CredentialObject
    {
        /// <summary>
        /// Gets or sets the referent (the credential id in the wallet).
        /// </summary>
        /// <value>
        /// The referent.
        /// </value>
        [JsonProperty("referent")]
        public string Referent { get; set; }

        /// <summary>
        /// Gets or sets the attributes and their raw values.
        /// </summary>
        /// <value>
        /// The attributes.
        /// </value>
        [JsonProperty("attrs")]
        public Dictionary<string, string> Attributes { get; set; }

        /// <summary>
        /// Gets or sets the schema identifier associated with this credential.
        /// </summary>
        /// <value>
        /// The schema identifier.
        /// </value>
        [JsonProperty("schema_id")]
        public string SchemaId { get; set; }

        /// <summary>
        /// Gets or sets the credential definition identifier associated with this credential.
        /// </summary>
        /// <value>
        /// The credential definition identifier.
        /// </value>
        [JsonProperty("cred_def_id")]
        public string CredentialDefinitionId { get; set; }

        /// <summary>
        /// Gets or sets the revocation registry identifier if supported by the definition, otherwise <c>null</c>.
        /// </summary>
        /// <value>
        /// The revocation registry identifier.
        /// </value>
        [JsonProperty("rev_reg_id")]
        public string RevocationRegistryId { get; set; }

        /// <summary>
        /// Gets or sets the credential revocation identifier if supported by the definition, otherwise <c>null</c>.
        /// </summary>
        /// <value>
        /// The credential revocation identifier.
        /// </value>
        [JsonProperty("cred_rev_id")]
        public string CredentialRevocationId { get; set; }
    }

    /// <summary>
    /// Represents a credential object as stored in the wallet.
    /// </summary>
    public class CredentialInfo
    {
        /// <summary>
        /// Gets or sets the credential object info.
        /// </summary>
        /// <value>The credential object.</value>
        [JsonProperty("cred_info")]
        public CredentialObject CredentialObject { get; set; }

        /// <summary>
        /// Gets or sets the non revocation interval for this credential.
        /// </summary>
        /// <value>The non revocation interval.</value>
        [JsonProperty("interval")]
        public RevocationInterval NonRevocationInterval { get; set; }
    }
}
