using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json.Linq;

namespace AzureFunctions
{
    public static class CandyBoxes
    {
        [FunctionName("OrderCandyBoxes")]
        public static async Task<string> Run(
            [OrchestrationTrigger] DurableOrchestrationContext shippingContext, TraceWriter log)
        {
            int minInventory = 100;
            ShippingBoxes input = shippingContext.GetInput<ShippingBoxes>();

            if (input.Volume > minInventory)
            {
                int procurmentTimeDuration = 1;

                DateTime procurmentTime = shippingContext.CurrentUtcDateTime.AddSeconds(procurmentTimeDuration);

                //Time Monitoring for Procurment
                while (shippingContext.CurrentUtcDateTime < procurmentTime)
                {
                    int requiredBoxes = input.Volume - minInventory;

                    //Chained Func.
                    bool jobStatus = await shippingContext.CallActivityAsync<bool>("PrepareBoxes", requiredBoxes);

                    if (jobStatus)
                    {
                        // Perform action when condition met
                        var tasks = new Task<long>[input.Volume];
                        for (int i = 0; i < input.Volume; i++)
                        {
                            tasks[i] = shippingContext.CallActivityAsync<long>(
                                "FillBoxes",
                                i);
                        }

                        await Task.WhenAll(tasks);

                        long totalWeight = tasks.Sum(t => t.Result);

                        //QC

                        if (totalWeight > 0)
                            return "---Recipt---|---Your FullFilment ID :" + Guid.NewGuid().ToString() + " ---|---Total Shipping Weight:" + totalWeight;
                        else
                            return "Your Order Failed QC";

                    }
                    // Orchestration will sleep until this time
                    var nextCheck = shippingContext.CurrentUtcDateTime.AddSeconds(procurmentTimeDuration);


                    //Add Monitor : To check Boxes Arrived
                    await shippingContext.CreateTimer(nextCheck, CancellationToken.None);
                }

                return "Time Elapsed but procurment did not arrive due to adhoc conditions";
            }
            else
            {
                //FAN OUT -> to process Candy Full Fillment
                var tasks = new Task<long>[input.Volume];
                for (int i = 0; i < input.Volume; i++)
                {
                    tasks[i] = shippingContext.CallActivityAsync<long>(
                        "FillBoxes",
                        i);
                }

                await Task.WhenAll(tasks);

                long totalWeight = tasks.Sum(t => t.Result);

                //QC : WEight Check

                if (totalWeight > 0)
                    return "---Recipt---|---Your FullFilment ID :" + Guid.NewGuid().ToString() + " ---|---Total Shipping Weight:" + totalWeight;
                else
                    return "Your Order Failed QC";
            }

        }

        [FunctionName("PrepareBoxes")]
        public static int GetFileList(
            [ActivityTrigger] int boxesRequired,
            TraceWriter log)
        {

            return boxesRequired;
        }

        public class ShippingBoxes
        {
            public int OrderId { get; set; }

            public int Volume { get; set; }
        }

        [FunctionName("FillBoxes")]
        public static async Task<long> FillBoxes(
            [ActivityTrigger] string filePath,
            Binder binder,
            TraceWriter log)
        {
            var items = new List<long>[100];
            var itemsWeight = 0;
            foreach (var item in items)
            {
                itemsWeight = itemsWeight + await GetItem();
            }
            return itemsWeight;
        }

        private static async Task<int> GetItem()
        {
            return 1;
        }
    }
}
