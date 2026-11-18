using System;
using System.Globalization;
using System.Xml;

namespace MegaDownloader
{

    /// <summary>Local-time download windows. No timers, I/O or queue mutations.</summary>
    public sealed class DownloadScheduleSettings
    {
        public bool Enabled;
        public bool Daily = true;
        public int StartMinute;
        public int EndMinute = 420;
        public DateTime OnceStart = DateTime.Today.AddDays(1d);
        public DateTime OnceEnd = DateTime.Today.AddDays(1d).AddHours(7d);

        public bool IsValid()
        {
            if (Daily)
                return StartMinute >= 0 && StartMinute < 1440 && EndMinute >= 0 && EndMinute < 1440 && StartMinute != EndMinute;
            return OnceEnd > OnceStart;
        }

        /// <summary>Window identity stays constant across midnight; end is exclusive.</summary>
        public string ActiveWindow(DateTime now)
        {
            if (!Enabled || !IsValid())
                return null;
            if (!Daily)
            {
                if (now >= OnceStart && now < OnceEnd)
                    return OnceStart.ToString("O", CultureInfo.InvariantCulture);
                return null;
            }
            double minute = now.TimeOfDay.TotalMinutes;
            var startDate = now.Date;
            if (StartMinute < EndMinute)
            {
                if (minute < StartMinute || minute >= EndMinute)
                    return null;
            }
            else
            {
                if (minute >= EndMinute && minute < StartMinute)
                    return null;
                if (minute < EndMinute)
                    startDate = startDate.AddDays(-1);
            }
            return startDate.AddMinutes(StartMinute).ToString("O", CultureInfo.InvariantCulture);
        }

        public bool AllowsDownload(DateTime now)
        {
            return !Enabled || ActiveWindow(now) is not null;
        }

        public XmlElement ToXml(XmlDocument document)
        {
            var node = document.CreateElement("DownloadSchedule");
            node.SetAttribute("Enabled", Enabled.ToString());
            node.SetAttribute("Daily", Daily.ToString());
            node.SetAttribute("StartMinute", StartMinute.ToString(CultureInfo.InvariantCulture));
            node.SetAttribute("EndMinute", EndMinute.ToString(CultureInfo.InvariantCulture));
            node.SetAttribute("OnceStart", OnceStart.ToString("O", CultureInfo.InvariantCulture));
            node.SetAttribute("OnceEnd", OnceEnd.ToString("O", CultureInfo.InvariantCulture));
            return node;
        }

        public static DownloadScheduleSettings FromXml(XmlNode node)
        {
            var result = new DownloadScheduleSettings();
            if (node is null || node.Attributes is null)
                return result;
            bool enabled;
            bool daily;
            int startMinute;
            var endMinute = default(int);
            DateTime onceStart;
            var onceEnd = default(DateTime);
            Func<string, string> getValue = name => node.Attributes[name] is null ? "" : node.Attributes[name].Value;
            if (!bool.TryParse(getValue("Enabled"), out enabled) || !bool.TryParse(getValue("Daily"), out daily))
                return result;
            result.Daily = daily;
            bool dailyValid = int.TryParse(getValue("StartMinute"), NumberStyles.Integer, CultureInfo.InvariantCulture, out startMinute) && int.TryParse(getValue("EndMinute"), NumberStyles.Integer, CultureInfo.InvariantCulture, out endMinute) && startMinute >= 0 && startMinute < 1440 && endMinute >= 0 && endMinute < 1440 && startMinute != endMinute;
            if (dailyValid)
            {
                result.StartMinute = startMinute;
                result.EndMinute = endMinute;
            }
            bool onceValid = DateTime.TryParseExact(getValue("OnceStart"), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out onceStart) && DateTime.TryParseExact(getValue("OnceEnd"), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out onceEnd) && onceEnd > onceStart;
            if (onceValid)
            {
                result.OnceStart = DateTime.SpecifyKind(onceStart, DateTimeKind.Unspecified);
                result.OnceEnd = DateTime.SpecifyKind(onceEnd, DateTimeKind.Unspecified);
            }
            if (daily && !dailyValid || !daily && !onceValid)
                return new DownloadScheduleSettings();
            result.Enabled = enabled && result.IsValid();
            return result;
        }
    }

    /// <summary>Edge-triggered actions; a manual pause lasts for the current window.</summary>
    public sealed class DownloadScheduleController
    {
        public enum ScheduleAction
        {
            None,
            Start,
            Pause
        }

        private string _lastWindow;
        private string _manualWindow;
        private readonly object stateLock = new object();

        public void Reset()
        {
            lock (stateLock)
            {
                _lastWindow = null;
                _manualWindow = null;
            }
        }

        public void MarkManualPause(DownloadScheduleSettings settings, DateTime now)
        {
            lock (stateLock)
                _manualWindow = settings.ActiveWindow(now);
        }

        public void MarkManualStart()
        {
            lock (stateLock)
                _manualWindow = null;
        }

        public ScheduleAction Evaluate(DownloadScheduleSettings settings, DateTime now, bool isDownloading)
        {
            lock (stateLock)
            {
                if (!settings.Enabled)
                {
                    Reset();
                    return ScheduleAction.None;
                }
                string window = settings.ActiveWindow(now);
                bool changed = !string.Equals(window, _lastWindow, StringComparison.Ordinal);
                _lastWindow = window;
                if (window is null)
                {
                    if (isDownloading)
                        return ScheduleAction.Pause;
                    return ScheduleAction.None;
                }
                if (changed && !string.Equals(window, _manualWindow, StringComparison.Ordinal) && !isDownloading)
                    return ScheduleAction.Start;
                return ScheduleAction.None;
            }
        }
    }
}