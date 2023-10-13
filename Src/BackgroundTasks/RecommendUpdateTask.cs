using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace JacobC.Xiami.Services
{
    /// <summary>
    /// Get the background update tasks recommended by Shrimp daily
    /// </summary>
    public sealed class RecommendUpdateTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Debug.WriteLine("[ex] Background tasks recommended by Shrimp daily: " 
                + "Not Implemented yet!");
            //throw new NotImplementedException();
        }
    }
}
