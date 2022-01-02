﻿using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using IdempotencyTools;
using Interactions;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace LogStore
{
    public class ComputeStatistics : BackgroundService
    {
        private ConfigurationPackage configurationPackage;
        private IReliableQueue<IdempotentMessage<PurchaseInfo>> queue;
        private IReliableStateManager stateManager;

        public ComputeStatistics(
            IReliableQueue<IdempotentMessage<PurchaseInfo>> queue,
            IReliableStateManager stateManager,
            ConfigurationPackage configurationPackage)
        {
            this.queue = queue;
            this.stateManager = stateManager;
            this.configurationPackage = configurationPackage;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queueEmpty = false;
            var delayString = configurationPackage.Settings.Sections["Timing"]
                .Parameters["MessageMaxDelaySeconds"].Value;
            var delay = int.Parse(delayString);
            var filter = await IdempotencyFilter.NewIdempotencyFilterAsync(
                "logMessages", delay, stateManager);
            var store = await
                stateManager.GetOrAddAsync<IReliableDictionary<string, RunningTotal>>("partialCount");
            while (!stoppingToken.IsCancellationRequested)
            {
                while (!queueEmpty && !stoppingToken.IsCancellationRequested)
                {
                    RunningTotal finalDayTotal = null;
                    using (var tx = stateManager.CreateTransaction())
                    {
                        var result = await queue.TryDequeueAsync(tx);
                        if (!result.HasValue)
                        {
                            queueEmpty = true;
                        }
                        else
                        {
                            var item = await filter.NewMessage<PurchaseInfo>(result.Value);
                            if (item != null)
                            {
                                var counter = await store.TryGetValueAsync(tx, item.Location);
                                var newCounter = counter.HasValue
                                    ? new RunningTotal
                                    {
                                        Count = counter.Value.Count,
                                        Day = counter.Value.Day
                                    }
                                    : new RunningTotal();
                                finalDayTotal = newCounter.Update(item.Time, item.Cost);
                                if (counter.HasValue)
                                    await store.TryUpdateAsync(tx, item.Location,
                                        newCounter, counter.Value);
                                else
                                    await store.TryAddAsync(tx, item.Location, newCounter);
                            }

                            await tx.CommitAsync();
                            if (finalDayTotal != null) await SendTotal(finalDayTotal, item.Location);
                        }
                    }
                }

                await Task.Delay(100, stoppingToken);
                queueEmpty = false;
            }
        }

        protected async Task SendTotal(RunningTotal total, string location)
        {
        }
    }
}