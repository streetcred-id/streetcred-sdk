﻿using System;
using System.Threading.Tasks;
using Streetcred.Sdk.Model.Records;
using Xunit;

namespace Streetcred.Sdk.Tests
{
    public class StateMachineTests
    {
        [Fact]
        public async Task CanTransitionFromDisconnetedToNegotiatingWithInvitationAccept()
        {
            var record = new ConnectionRecord();

            Assert.True(ConnectionState.Invited == record.State);

            await record.TriggerAsync(ConnectionTrigger.InvitationAccept);

            Assert.True(ConnectionState.Negotiating == record.State);
        }

        [Fact]
        public async Task CanTransitionFromInvitedToConnectedWithRequest()
        {
            var record = new ConnectionRecord() { State = ConnectionState.Invited };

            Assert.True(ConnectionState.Invited == record.State);

            await record.TriggerAsync(ConnectionTrigger.Request);

            Assert.True(ConnectionState.Connected == record.State);
        }

        [Fact]
        public async Task CanTransitionFromNegotiatingToConnectedWithRespone()
        {
            var record = new ConnectionRecord() { State = ConnectionState.Negotiating };

            Assert.True(ConnectionState.Negotiating == record.State);

            await record.TriggerAsync(ConnectionTrigger.Response);

            Assert.True(ConnectionState.Connected == record.State);
        }

        [Fact]
        public async Task CannotTransitionFromNegotiatingToConnectedWithRequest()
        {
            var record = new ConnectionRecord { State = ConnectionState.Negotiating};

            Assert.True(ConnectionState.Negotiating == record.State);

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => record.TriggerAsync(ConnectionTrigger.Request));

            Assert.Equal("Stateless", exception.Source);
        }

        [Fact]
        public async Task CannotTransitionFromInvitedWithAccept()
        {
            var record = new ConnectionRecord { State = ConnectionState.Negotiating};

            Assert.True(ConnectionState.Negotiating == record.State);

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => record.TriggerAsync(ConnectionTrigger.InvitationAccept));

            Assert.Equal("Stateless", exception.Source);
        }
    }
}