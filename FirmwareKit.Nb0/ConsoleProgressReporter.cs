using System;

namespace FirmwareKit.Nb0
{
    /// <summary>
    /// <para>将进度信息输出到控制台的进度报告器实现。</para>
    /// A progress reporter implementation that outputs progress information to the console.
    /// </summary>
    /// <remarks>
    /// <para>当进度百分比变化或处理到最后一个条目时，将进度信息写入控制台。</para>
    /// Writes progress information to the console when the progress percentage changes or the last entry is reached.
    /// </remarks>
    public class ConsoleProgressReporter : IProgressReporter
    {
        private int _lastProgressLine = -1;

        /// <summary>
        /// <para>将进度报告输出到控制台。仅在进度百分比变化或处理完成时输出。</para>
        /// Outputs the progress report to the console. Only outputs when the progress percentage changes or processing is complete.
        /// </summary>
        /// <param name="report">
        /// <para>包含当前进度详细信息的 <see cref="ProgressReport"/> 实例。</para>
        /// The <see cref="ProgressReport"/> instance containing current progress details.
        /// </param>
        public void Report(ProgressReport report)
        {
            int progressLine = (int)report.Progress;
            if (progressLine != _lastProgressLine || report.CurrentIndex == report.TotalCount)
            {
                _lastProgressLine = progressLine;
                Console.WriteLine(report.ToString());
            }
        }
    }
}
