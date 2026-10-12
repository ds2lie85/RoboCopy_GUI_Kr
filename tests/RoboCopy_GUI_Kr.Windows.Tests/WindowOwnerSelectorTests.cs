using System.Windows;

namespace RoboCopy_GUI_Kr.Windows.Tests;

internal static class WindowOwnerSelectorTests
{

    internal static void VerifyMiniStartupOwners()
    {


        {
            var main = new Window { Left = -30_000, Top = -30_000, ShowActivated = false, ShowInTaskbar = false };
            var mini = new Window { Left = -30_000, Top = -30_000, ShowActivated = false, ShowInTaskbar = false };
            var review = new Window();
            try
            {
                Assert.Throws<InvalidOperationException>(() => review.Owner = main);
                Assert.Null(WindowOwnerSelector.Select(main, mini));
                mini.Show();
                review.Owner = WindowOwnerSelector.Select(main, mini);
                Assert.Same(mini, review.Owner);
                main.Show();
                review.Owner = WindowOwnerSelector.Select(main, mini);
                Assert.Same(main, review.Owner);
                main.Hide();
                review.Owner = WindowOwnerSelector.Select(main, mini);
                Assert.Same(mini, review.Owner);
                review.Owner = null;
                mini.Close();
                review.Owner = WindowOwnerSelector.Select(main, mini);
                Assert.Null(review.Owner);
            }

            finally { review.Close(); mini.Close(); main.Close(); }
        }




    }
}
