﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Exceptions;
using AgentFramework.Core.Messages;
using AgentFramework.Core.Messages.Credentials;
using AgentFramework.Core.Messages.Proofs;
using Newtonsoft.Json;

namespace AgentFramework.Core.Agents.Default
{
    public class ProofHandler : IHandler
    {
        private readonly IProofService _proofService;

        public ProofHandler(IProofService proofService)
        {
            _proofService = proofService;
        }

        public IEnumerable<string> SupportedMessageTypes => new[]
        {
            MessageTypes.ProofRequest,
            MessageTypes.DisclosedProof
        };

        public async Task OnMessageAsync(string agentMessage, AgentContext context)
        {
            var message = JsonConvert.DeserializeObject<IAgentMessage>(agentMessage);

            switch (message)
            {
                case ProofRequestMessage request:
                    await _proofService.ProcessProofRequestAsync(context.Wallet, request, context.Connection);
                    break;

                case ProofMessage proof:
                    await _proofService.ProcessProofAsync(context.Wallet, proof, context.Connection);
                    break;
            }

            throw new AgentFrameworkException(ErrorCode.InvalidMessage, $"Unsupported message type {message.Type}");
        }
    }
}
