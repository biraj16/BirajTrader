// In TradingConsole.Tests/MainViewModelTests.cs

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Tests
{
    [TestClass]
    public class MainViewModelTests
    {
        [TestMethod]
        public void PnlProperties_ShouldCalculateCorrectly_WithMultiplePositions()
        {
            // --- ARRANGE ---
            // Create the ViewModel. We still use dummy values for the constructor
            // as we are not testing API connectivity here.
            var viewModel = new MainViewModel("dummy_id", "dummy_token");

            // --- Corrected Setup for Open Positions ---
            // To test the calculated UnrealizedPnl, we now set the properties
            // that are used in its calculation.

            // MODIFIED: Access OpenPositions through the new Portfolio property
            viewModel.Portfolio.OpenPositions.Add(new Position
            {
                Quantity = 10,
                AveragePrice = 100,
                LastTradedPrice = 115 // PnL = 10 * (115 - 100) = 150
            });

            // MODIFIED: Access OpenPositions through the new Portfolio property
            viewModel.Portfolio.OpenPositions.Add(new Position
            {
                Quantity = 5,
                AveragePrice = 200,
                LastTradedPrice = 190 // PnL = 5 * (190 - 200) = -50
            });


            // MODIFIED: Access ClosedPositions through the new Portfolio property
            viewModel.Portfolio.ClosedPositions.Add(new Position { RealizedPnl = 200m });
            viewModel.Portfolio.ClosedPositions.Add(new Position { RealizedPnl = -75m });


            // --- ACT ---
            // The "Act" is simply reading the values of the calculated properties in the ViewModel.
            // MODIFIED: Access PnL properties through the new Portfolio property
            decimal openPnl = viewModel.Portfolio.OpenPnl;
            decimal bookedPnl = viewModel.Portfolio.BookedPnl;
            decimal netPnl = viewModel.Portfolio.NetPnl;


            // --- ASSERT ---
            // Verify the final calculations based on our new arrangement.
            // Expected Open PnL: 150 + (-50) = 100
            // Expected Booked PnL: 200 + (-75) = 125
            // Expected Net PnL: 100 + 125 = 225
            Assert.AreEqual(100m, openPnl, "Open PnL was not calculated correctly.");
            Assert.AreEqual(125m, bookedPnl, "Booked PnL was not calculated correctly.");
            Assert.AreEqual(225m, netPnl, "Net PnL was not calculated correctly.");
        }
    }
}
