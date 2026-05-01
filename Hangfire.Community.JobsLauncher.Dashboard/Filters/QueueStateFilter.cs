using Hangfire.Common;
using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Hangfire.Community.JobsLauncher.Dashboard.Filters
{
    public class QueueStateFilter : JobFilterAttribute, IApplyStateFilter
    {
        public new int Order { get; set; } = 0;
        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            // Cuando el job se encola, revisa si se definió una cola personalizada
            if (context.NewState is EnqueuedState enqueuedState)
            {
                string originalQueue = context.Connection.GetJobParameter(context.BackgroundJob.Id, ParametersNameJobs.Queue);
                if (originalQueue != null)
                {
                    // Si se encuentra el parámetro "Queue", se asigna la cola original al estado EnqueuedState
                    enqueuedState.Queue = originalQueue;
                }
                else
                {
                    // Si no se encuentra el parámetro "Queue",
                    // se alimenta el job con el valor de la cola del estado EnqueuedState con el fin de redirigir a la misma cola en caso de reintentos
                    context.Connection.SetJobParameter(context.BackgroundJob.Id, ParametersNameJobs.Queue, enqueuedState.Queue);
                }
            }
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            // No se requiere acción al desaplicar el estado
        }
    }
}
