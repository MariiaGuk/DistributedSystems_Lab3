using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MulticastDelegate
{
    // Делегат, що описує сигнатуру методів форматування числа
    public delegate void FormatNumber(double number);

    public partial class MainWindow : Window
    {
        // Об'єкт синхронізації для запису в журнал з різних потоків
        private readonly object logLocker = new object();
        private int lineNumber = 0;

        // Кольори для журналу
        private static readonly Color ColHeader = Color.FromRgb(0xC5, 0x86, 0xC0); // заголовки
        private static readonly Color ColTotal = Color.FromRgb(0xDC, 0xDC, 0xAA); // підсумок
        private static readonly Color ColCurrency = Color.FromRgb(0x4E, 0xC9, 0xB0); // Currency
        private static readonly Color ColCommas = Color.FromRgb(0x56, 0x9C, 0xD6); // Commas
        private static readonly Color ColPlaces = Color.FromRgb(0xCE, 0x91, 0x78); // 3 places
        private static readonly Color ColStart = Color.FromRgb(0x80, 0x80, 0x80); // "початок"
        private static readonly Color ColNumber = Color.FromRgb(0x6E, 0x6E, 0x6E); // номер рядка

        // Кольори міток потоків (за Id потоку)
        private static readonly Color[] ThreadColors =
        {
            Color.FromRgb(0xF4, 0x47, 0x47), Color.FromRgb(0xB5, 0xCE, 0xA8),
            Color.FromRgb(0xFF, 0xC6, 0x6D), Color.FromRgb(0x9C, 0xDC, 0xFE),
            Color.FromRgb(0xD7, 0xBA, 0x7D), Color.FromRgb(0xC5, 0x86, 0xC0)
        };

        // Штучна затримка кожного методу (мс), імітує тривалу роботу
        private int delayMs = 1000;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Побудова мультикаст-делегата

        private FormatNumber BuildDelegate()
        {
            FormatNumber format = null;

            // Групове перетворення методів та оператор += (мультикаст)
            if (chkCurrency.IsChecked == true) format += FormatNumberAsCurrency;
            if (chkCommas.IsChecked == true) format += FormatNumberWithCommas;
            if (chkPlaces.IsChecked == true) format += FormatNumberWithTwoPlaces;

            return format;
        }

        private bool ReadInput(out double value, out FormatNumber format)
        {
            format = BuildDelegate();
            value = 0;

            if (format == null)
            {
                MessageBox.Show("Оберіть хоча б один метод.", "Увага",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string text = txtNumber.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value))
            {
                MessageBox.Show("Введіть коректне число, наприклад 12345.6789.", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!int.TryParse(txtDelay.Text.Trim(), out delayMs) || delayMs < 0)
            {
                MessageBox.Show("Затримка має бути невід'ємним цілим числом (мс).", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        // Послідовний виклик у головному (UI) потоці

        private void btnSequential_Click(object sender, RoutedEventArgs e)
        {
            double value;
            FormatNumber format;
            if (!ReadInput(out value, out format)) return;

            SetButtons(false);
            lblStatus.Text = "Виконується послідовний виклик...";
            AppendLog("=== Послідовний виклик (усі методи в одному потоці) ===", ColHeader, true);

            // Увесь мультикаст виконується одним робочим потоком,
            // щоб інтерфейс не "завис" через затримку
            Thread worker = new Thread(() =>
            {
                DateTime start = DateTime.Now;
                format(value); // методи викликаються по черзі
                double ms = (DateTime.Now - start).TotalMilliseconds;

                AppendLog("Готово. Загальний час: " + ms.ToString("F0") + " мс", ColTotal, true);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    lblStatus.Text = "Послідовний виклик завершено.";
                    SetButtons(true);
                }));
            });
            worker.Start();
        }

        // Паралельний виклик: кожен метод у власному потоці

        private void btnParallel_Click(object sender, RoutedEventArgs e)
        {
            double value;
            FormatNumber format;
            if (!ReadInput(out value, out format)) return;

            SetButtons(false);
            lblStatus.Text = "Потоки виконуються...";
            AppendLog("=== Паралельний виклик, головний потік (Id = " +
                Thread.CurrentThread.ManagedThreadId + ") ===", ColHeader, true);

            Delegate[] methods = format.GetInvocationList();
            Thread[] threads = new Thread[methods.Length];
            DateTime start = DateTime.Now;

            for (int i = 0; i < methods.Length; i++)
            {
                // Локальна копія, щоб кожен потік отримав власний метод
                FormatNumber method = (FormatNumber)methods[i];
                threads[i] = new Thread(() => method(value));
                threads[i].Start();
            }

            // Очікування потоків виконується в окремому потоці,
            // щоб не блокувати інтерфейс
            Thread waiter = new Thread(() =>
            {
                foreach (Thread t in threads)
                    t.Join();

                double ms = (DateTime.Now - start).TotalMilliseconds;
                AppendLog("Усі потоки завершено. Загальний час: " + ms.ToString("F0") + " мс", ColTotal, true);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    lblStatus.Text = "Паралельний виклик завершено.";
                    SetButtons(true);
                }));
            });
            waiter.Start();
        }

        // Методи-делегати

        private void FormatNumberAsCurrency(double number)
        {
            AppendLog("A Currency: початок", ColStart);
            Thread.Sleep(delayMs); // штучна затримка
            AppendLog("A Currency: " + number.ToString("C"), ColCurrency, true);
        }

        private void FormatNumberWithCommas(double number)
        {
            AppendLog("With Commas: початок", ColStart);
            Thread.Sleep(delayMs);
            AppendLog("With Commas: " + number.ToString("N"), ColCommas, true);
        }

        private void FormatNumberWithTwoPlaces(double number)
        {
            AppendLog("With 3 places: початок", ColStart);
            Thread.Sleep(delayMs);
            AppendLog("With 3 places: " + number.ToString(".###"), ColPlaces, true);
        }

        // Потокобезпечний запис у журнал

        private void AppendLog(string message, Color color, bool bold = false)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            int number;
            lock (logLocker) // нумерація рядків без гонки даних
            {
                lineNumber++;
                number = lineNumber;
            }

            Color threadColor = ThreadColors[threadId % ThreadColors.Length];

            // Елементи інтерфейсу можна змінювати лише з UI-потоку
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Paragraph p = new Paragraph();
                p.Margin = new Thickness(0);

                p.Inlines.Add(new Run(string.Format("{0,3}. ", number))
                { Foreground = new SolidColorBrush(ColNumber) });
                p.Inlines.Add(new Run(string.Format("[потік {0}] ", threadId))
                { Foreground = new SolidColorBrush(threadColor) });

                Run text = new Run(message) { Foreground = new SolidColorBrush(color) };
                if (bold) text.FontWeight = FontWeights.Bold;
                p.Inlines.Add(text);

                rtbLog.Document.Blocks.Add(p);
                rtbLog.ScrollToEnd();
            }));
        }

        private void SetButtons(bool enabled)
        {
            btnSequential.IsEnabled = enabled;
            btnParallel.IsEnabled = enabled;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            rtbLog.Document.Blocks.Clear();
            lock (logLocker) { lineNumber = 0; }
            lblStatus.Text = "Журнал очищено.";
        }
    }
}