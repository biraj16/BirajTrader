// In TradingConsole.Tests/LoginViewModelTests.cs

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Tests
{
    [TestClass] // This attribute tells MSTest that this class contains tests
    public class LoginViewModelTests
    {
        [TestMethod] // This attribute marks this method as a single test case
        public void LoginCommand_ShouldBeDisabled_WhenCredentialsAreMissing()
        {
            // --- ARRANGE ---
            // Create an instance of the ViewModel we want to test.
            var viewModel = new LoginViewModel();

            // Set only one of the required properties.
            viewModel.AccessToken = "a_valid_token";
            // ClientId is left empty.


            // --- ACT ---
            // Execute the logic we are testing. In this case, we're checking the
            // CanExecute predicate of the LoginCommand.
            bool canLogin = viewModel.LoginCommand.CanExecute(null);


            // --- ASSERT ---
            // Verify that the result is what we expect. We expect the command
            // to be disabled, so CanExecute should be false.
            Assert.IsFalse(canLogin, "Login command should be disabled if ClientId is missing.");
        }

        [TestMethod]
        public void LoginCommand_ShouldBeEnabled_WhenCredentialsAreProvided()
        {
            // --- ARRANGE ---
            // Create the ViewModel.
            var viewModel = new LoginViewModel();

            // Set all the required properties to valid states.
            viewModel.ClientId = "a_valid_client_id";
            viewModel.AccessToken = "a_valid_token";


            // --- ACT ---
            // Execute the CanExecute logic.
            bool canLogin = viewModel.LoginCommand.CanExecute(null);


            // --- ASSERT ---
            // Verify that the command is now enabled.
            Assert.IsTrue(canLogin, "Login command should be enabled when both ClientId and AccessToken are provided.");
        }
    }
}