using System;
using System.IO;
using System.Text;
using System.Windows;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Catspaw
{
    /// <summary>
    /// Define a bindable serilog sink writing in a StringBuilder. The Text property is a dependency property.
    /// Derived from DependencyObject and implement IlogEventSink interface
    /// </summary>
    public class StringSink : DependencyObject, ILogEventSink
    {
        private const int lineLength = 256;
        private const int maxLineCount = 100000;

        private readonly StringBuilder text;
        private readonly MessageTemplateTextFormatter formatter;

        /// <summary>
        /// Create the String Sink with default output template and format provider
        /// </summary>
        /// <param name="outputTemplate">The tempalte used to ouput the message</param>
        /// <param name="formatProvider">The format provider</param>
        public StringSink(
            string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}",
            IFormatProvider formatProvider = null)
        {
            text = new StringBuilder(lineLength * maxLineCount);
            formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        }

        /// <summary>
        /// Set or get the text of the Sink. The given text is always append to the end of the sink.
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, text.Append(value).ToString());
        }

        /// <summary>
        /// The dependency property associated to Text
        /// </summary>
        public static readonly DependencyProperty TextProperty = 
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(StringSink));

        /// <summary>
        /// Implement the ILogEventSink interface. Append a formatted message to the sink.  
        /// </summary>
        /// <param name="logEvent">The log event to append</param>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            using var message = new StringWriter();
            formatter.Format(logEvent, message);
            message.Flush();
            // We need to be on the dispatcher thread to modify the dependency property
            if (Application.Current.Dispatcher.CheckAccess()) Text = message.ToString();
            else Application.Current.Dispatcher.Invoke(new Action(() => Text = message.ToString()));
        }
    }
}

