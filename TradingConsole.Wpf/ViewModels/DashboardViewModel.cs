using System.Collections.ObjectModel;
using System.Linq;
using TradingConsole.Core.Models;
using TradingConsole.DhanApi.Models.WebSocket;

namespace TradingConsole.Wpf.ViewModels
{
    public class DashboardViewModel
    {
        public ObservableCollection<DashboardInstrument> MonitoredInstruments { get; } = new ObservableCollection<DashboardInstrument>();

        public DashboardViewModel()
        {
            // The list is populated by the MainViewModel.
        }

        public void UpdateLtp(TickerPacket packet)
        {
            var instrument = MonitoredInstruments.FirstOrDefault(i => i.SecurityId == packet.SecurityId);
            if (instrument != null)
            {
                instrument.LTP = packet.LastPrice;
            }
        }

        public void UpdateQuote(QuotePacket packet)
        {
            var instrument = MonitoredInstruments.FirstOrDefault(i => i.SecurityId == packet.SecurityId);
            if (instrument != null)
            {
                instrument.LTP = packet.LastPrice;
                instrument.Open = packet.Open;
                instrument.High = packet.High;
                instrument.Low = packet.Low;
                instrument.Close = packet.Close;
                instrument.Volume = packet.Volume;
                instrument.LastTradedQuantity = packet.LastTradeQuantity;
                instrument.LastTradeTime = packet.LastTradeTime;
                instrument.AvgTradePrice = packet.AvgTradePrice;
            }
        }

        public void UpdateOi(OiPacket packet)
        {
            var instrument = MonitoredInstruments.FirstOrDefault(i => i.SecurityId == packet.SecurityId);
            if (instrument != null)
            {
                instrument.OpenInterest = packet.OpenInterest;
            }
        }

        public void UpdatePreviousClose(PreviousClosePacket packet)
        {
            var instrument = MonitoredInstruments.FirstOrDefault(i => i.SecurityId == packet.SecurityId);
            if (instrument != null)
            {
                instrument.Close = packet.PreviousClose;
            }
        }
    }
}
