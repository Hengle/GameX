using System;
using System.ComponentModel;

namespace GameX.FileSystems.Casc
{
    public class BackgroundWorkerEx : BackgroundWorker
    {
        private int lastProgressPercentage;

        public BackgroundWorkerEx()
        {
            WorkerReportsProgress = true;
            WorkerSupportsCancellation = true;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            try
            {
                base.OnDoWork(e);
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        public new void ReportProgress(int percentProgress)
        {
            if (CancellationPending)
                throw new OperationCanceledException();

            if (IsBusy && percentProgress > lastProgressPercentage)
                base.ReportProgress(percentProgress);

            lastProgressPercentage = percentProgress;
        }

        public new void ReportProgress(int percentProgress, object userState)
        {
            if (CancellationPending)
                throw new OperationCanceledException();

            if (IsBusy)
                base.ReportProgress(percentProgress, userState);

            lastProgressPercentage = percentProgress;
        }
    }
}
