﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ServiceFabric;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace Microsoft.PSharp.ReliableServices
{
    /// <summary>
    /// Object hosting an RSM
    /// </summary>
    public sealed class ServiceFabricRsmHost : RsmHost
    {
        /// <summary>
        /// Persistent current state (stack)
        /// </summary>
        private IReliableDictionary<int, string> StateStackStore;

        /// <summary>
        /// Inbox
        /// </summary>
        private IReliableConcurrentQueue<Event> InputQueue;

        /// <summary>
        /// RSMs created by this host
        /// </summary>
        private IReliableDictionary<IRsmId, Tuple<string, RsmInitEvent>> CreatedMachines;

        /// <summary>
        /// Machines that haven't been started yet
        /// </summary>
        private Dictionary<IRsmId, Tuple<string, RsmInitEvent>> PendingMachineCreations;

        /// <summary>
        /// For creating unique RsmIds
        /// </summary>
        private ServiceFabricRsmIdFactory IdFactory;

        /// <summary>
        /// Has the machine halted
        /// </summary>
        private bool MachineHalted;

        /// <summary>
        /// Machine failed with an exception
        /// </summary>
        private Exception MachineFailureException;

        private ServiceFabricRsmHost(IReliableStateManager stateManager, ServiceFabricRsmId id, ServiceFabricRsmIdFactory factory)
            : base(stateManager)
        {
            this.Id = id;
            this.IdFactory = factory;
            this.CreatedMachines = null;
            this.PendingMachineCreations = new Dictionary<IRsmId, Tuple<string, RsmInitEvent>>();

            MachineHalted = false;
            MachineFailureException = null;
        }

        internal static RsmHost Create(IReliableStateManager stateManager, ServiceFabricRsmIdFactory factory)
        {
            var id = factory.Generate("Root");
            return new ServiceFabricRsmHost(stateManager, id, factory);
        }

        private async Task Initialize(Type machineType, RsmInitEvent ev)
        {
            InputQueue = await StateManager.GetOrAddAsync<IReliableConcurrentQueue<Event>>(GetInputQueueName(this.Id));
            StateStackStore = await StateManager.GetOrAddAsync<IReliableDictionary<int, string>>(string.Format("StateStackStore.{0}", this.Id.Name));

            Runtime = PSharpRuntime.Create();

            RunMachine(machineType, ev);
        }

        private void RunMachine(Type machineType, RsmInitEvent ev)
        {
            Task.Run(async () =>
            {
                bool firstExecution = true;

                while (true)
                {
                    await EventHandlerLoop(machineType, ev, firstExecution);
                    firstExecution = false;

                    if (MachineHalted)
                    {
                        return;
                    }
                    // Inbox empty, wait
                    await Task.Delay(1000);
                }
            });
        }

        private async Task InitializationTransaction(Type machineType, RsmInitEvent ev)
        {
            var stack = new List<string>();

            var cnt = await StateStackStore.GetCountAsync(CurrentTransaction);
            if (cnt != 0)
            {
                for (int i = 0; i < cnt; i++)
                {
                    var s = await StateStackStore.TryGetValueAsync(CurrentTransaction, i);
                    stack.Add(s.Value);
                }

                this.Mid = await Runtime.CreateMachineAndExecute(machineType, new ResumeEvent(stack, new RsmInitEvent(this)));
            }
            else
            {
                this.Mid = await Runtime.CreateMachineAndExecute(machineType, ev);
            }

            if (MachineFailureException != null &&
                (MachineFailureException is TimeoutException || MachineFailureException is System.Fabric.TransactionFaultedException))
            {
                throw MachineFailureException;
            }

        }

        private async Task EventHandlerLoop(Type machineType, RsmInitEvent ev, bool firstExecution)
        {
            var machineRestartRequired = firstExecution;

            // TODO: retry policy
            while (!MachineHalted)
            {
                try
                {
                    var writeTx = false;
                    var inboxEmpty = false;

                    using (CurrentTransaction = StateManager.CreateTransaction())
                    {
                        if(firstExecution)
                        {
                            await ReadPendingWorkOnStart();
                            writeTx = false;
                        }
                        else if (machineRestartRequired)
                        {
                            machineRestartRequired = false;
                            await InitializationTransaction(machineType, ev);
                            await PersistStateStack();
                            writeTx = true;
                        }
                        else
                        {
                            inboxEmpty = await EventHandler();
                            var stackChanged = await PersistStateStack();
                            writeTx = (inboxEmpty || stackChanged);
                        }

                        if (writeTx)
                        {
                            await CurrentTransaction.CommitAsync();
                        }
                    }

                    StackChanges = new StackDelta();
                    await ExecutePendingWork();
                    firstExecution = false;

                    if (inboxEmpty)
                    {
                        return;
                    }
                }
                catch (Exception ex) when (ex is TimeoutException || ex is System.Fabric.TransactionFaultedException)
                {
                    MachineFailureException = null;
                    MachineHalted = false;
                    machineRestartRequired = true;

                    StackChanges = new StackDelta();
                    PendingMachineCreations.Clear();

                    // retry
                    await Task.Delay(100);
                    continue;
                }
            }
        }

        private async Task<bool> EventHandler()
        {
            var cv = await InputQueue.TryDequeueAsync(CurrentTransaction);
            if (!cv.HasValue)
            {
                return false;
            }

            var ev = cv.Value;
            await Runtime.SendEventAndExecute(Mid, ev);

            if (MachineFailureException != null &&
                (MachineFailureException is TimeoutException || MachineFailureException is System.Fabric.TransactionFaultedException))
            {
                throw MachineFailureException;
            }

            return true;
        }

        private async Task<bool> PersistStateStack()
        {
            if (StackChanges.PopDepth == 0 && StackChanges.PushedSuffix.Count == 0)
            {
                return false;
            }

            var cnt = (int) await StateStackStore.GetCountAsync(CurrentTransaction);
            for (int i = cnt - 1; i > cnt - 1 - StackChanges.PopDepth; i--)
            {
                await StateStackStore.TryRemoveAsync(CurrentTransaction, i);
            }

            for (int i = 0; i < StackChanges.PushedSuffix.Count; i++)
            {
                await StateStackStore.AddAsync(CurrentTransaction, i + (cnt - StackChanges.PopDepth), 
                    StackChanges.PushedSuffix[i]);
            }

            return true;
        }

        private async Task ExecutePendingWork()
        {
            // machine creations
            foreach(var tup in PendingMachineCreations)
            {
                var host = new ServiceFabricRsmHost(this.StateManager, tup.Key as ServiceFabricRsmId, this.IdFactory);
                await host.Initialize(Type.GetType(tup.Value.Item1), tup.Value.Item2);
            }

            PendingMachineCreations.Clear();
        }

        private async Task ReadPendingWorkOnStart()
        {
            CreatedMachines = await StateManager.GetOrAddAsync<IReliableDictionary<IRsmId, Tuple<string, RsmInitEvent>>>(
                GetCreatedMachineMapName(this.Id));

            var enumerable = await CreatedMachines.CreateEnumerableAsync(CurrentTransaction);
            var enumerator = enumerable.GetAsyncEnumerator();

            // TODO: Add cancellation token
            var ct = new System.Threading.CancellationToken();

            while (await enumerator.MoveNextAsync(ct))
            {
                PendingMachineCreations.Add(enumerator.Current.Key, enumerator.Current.Value);
            }
        }

        public override async Task<IRsmId> ReliableCreateMachine<T>(RsmInitEvent startingEvent)
        {
            if(CreatedMachines == null)
            {
                CreatedMachines = await StateManager.GetOrAddAsync<IReliableDictionary<IRsmId, Tuple<string, RsmInitEvent>>>(
                    GetCreatedMachineMapName(this.Id));
            }

            var id = IdFactory.Generate(typeof(T).Name);

            await CreatedMachines.AddAsync(CurrentTransaction, id, 
                Tuple.Create(typeof(T).AssemblyQualifiedName, startingEvent));

            PendingMachineCreations.Add(id, Tuple.Create(typeof(T).AssemblyQualifiedName, startingEvent));

            return id;
        }

        public override Task<IRsmId> ReliableCreateMachine<T>(RsmInitEvent startingEvent, string partitionName)
        {
            throw new NotImplementedException();
        }

        public override async Task ReliableSend(IRsmId target, Event e)
        {
            if (target.PartitionName != this.Id.PartitionName)
            {
                throw new NotImplementedException();
            }

            var targetQueue = await StateManager.GetOrAddAsync<IReliableConcurrentQueue<Event>>(GetInputQueueName(target));
            await targetQueue.EnqueueAsync(CurrentTransaction, e);
        }

        internal override void NotifyFailure(Exception ex, string methodName)
        {
            MachineFailureException = ex;
        }

        internal override void NotifyHalt()
        {
            MachineHalted = true;
        }

        static string GetInputQueueName(IRsmId id)
        {
            return string.Format("InputQueue.{0}", id.Name);
        }

        static string GetCreatedMachineMapName(IRsmId id)
        {
            return string.Format("CreatedMachines.{0}", id.Name);
        }
    }
}