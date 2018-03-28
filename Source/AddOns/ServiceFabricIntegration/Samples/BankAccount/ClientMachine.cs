﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PSharp;
using Microsoft.PSharp.ReliableServices;
using Microsoft.PSharp.ReliableServices.Utilities;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace BankAccount
{
    class ClientMachine : ReliableStateMachine
    {

        /// <param name="stateManager"></param>
        public ClientMachine(IReliableStateManager stateManager)
            : base(stateManager) { }

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        class Init : MachineState { }

        async Task InitOnEntry()
        {
            var acc1 = await this.ReliableCreateMachine(typeof(AccountMachine), null, new InitializeAccountEvent("A", 100));
            var acc2 = await this.ReliableCreateMachine(typeof(AccountMachine), null, new InitializeAccountEvent("B", 100));

            var amount = this.RandomInteger(150);

            var ev = new InitializeBrokerEvent(this.Id, acc1, acc2, amount);

            this.Monitor<SafetyMonitor>(ev);
            this.Monitor<SafetyMonitor>(new AccountBalanceUpdatedEvent(acc1, 100));
            this.Monitor<SafetyMonitor>(new AccountBalanceUpdatedEvent(acc2, 100));

            var broker = await this.ReliableCreateMachine(typeof(BrokerMachine), null, ev);

            await this.ReliableSend(acc2, new EnableEvent());
            await this.ReliableSend(acc1, new EnableEvent());
        }

        public override Task OnActivate()
        {
            return Task.CompletedTask;
        }
    }
}