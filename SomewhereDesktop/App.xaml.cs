using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SomewhereDesktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Close existing application
            Application.Current.MainWindow.Close();

            // Show new dialog
            StringBuilder builder = new StringBuilder(e.Exception.Message);
            builder.AppendLine();
            builder.AppendLine("The exception occured at following place: ");
            builder.AppendLine("```");
            builder.AppendLine(e.Exception.StackTrace);
            builder.AppendLine("```");
            builder.AppendLine("This is caused either by an illegal operation, most likely (if it's related to SQLite database) you have forgotten " +
                "to close an open connection to your database and two instances are trying to write to the same database at the same time - this is not allowed. " +
                "Or there is some issue that happened on our end (i.e. coding problem). " +
                "If you believe the issue is on our end, please provide detailed exception information (including exception message and stack trace above, and " +
                "details regarding how you encountered this error. It will be better if you can provide a way to recreate this issue using a clean repository.");
            builder.AppendLine("Don't panic! This may not be bad news, at least the application is not crashing, you can rest assured not " +
                "all of your data is lost - if any is lost at all.");
            builder.AppendLine();
            builder.AppendLine("Action Items: \n");
            builder.AppendLine("* To submit an issue, go to [Github Issue page](https://github.com/szinubuntu/Somewhere/issues);");
            builder.AppendLine("* You may also send email to developer directly if you are not sure what to do: [Charles Zhang](mailto:charles@totalimagine.com);");
            builder.AppendLine("* Just close this window and try again.");
            builder.AppendLine();
            builder.AppendLine("Thanks for your patience and support!");
            new DialogWindow(null, "Don't panic! An operation error has occured", builder.ToString()).ShowDialog();
        }
    }
}
