namespace SimpleTGBot;
using System.IO;
using Dangl.Calculator;
using Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using System;
using System.Collections.Generic;

using System.Threading.Tasks;


public class TelegramBot
{
    // Токен TG-бота. 
    private const string BotToken = "7010316766:AAEAMjFTSPz7NphYaJMk-uPwFvK7MfQHeBY";

    //состояние бота для каждого чата, одно но - обнуляется при перезапуске бота 
    public Dictionary<long, bool> status = new Dictionary<long, bool>();


    /// <summary>
    /// Инициализирует и обеспечивает работу бота до нажатия клавиши Esc
    /// </summary>
    public async Task Run()
    {
        // Инициализируем наш клиент, передавая ему токен.
        var botClient = new TelegramBotClient(BotToken);

        // Служебные вещи для организации правильной работы с потоками
        using CancellationTokenSource cts = new CancellationTokenSource();

        // Разрешённые события, которые будет получать и обрабатывать наш бот.
        // Будем получать только сообщения. При желании можно поработать с другими событиями.
        ReceiverOptions receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        // Привязываем все обработчики и начинаем принимать сообщения для бота
        botClient.StartReceiving(
            updateHandler: OnMessageReceived,
            pollingErrorHandler: OnErrorOccured,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        // Проверяем что токен верный и получаем информацию о боте
        var me = await botClient.GetMeAsync(cancellationToken: cts.Token);
        Console.WriteLine($"Бот @{me.Username} запущен.\nДля остановки нажмите клавишу Esc...");

        // Ждём, пока будет нажата клавиша Esc, тогда завершаем работу бота
        while (Console.ReadKey().Key != ConsoleKey.Escape) { }

        // Отправляем запрос для остановки работы клиента.
        cts.Cancel();
    }

    /// <summary>
    /// Обработчик события получения сообщения.
    /// </summary>
    /// <param name="botClient">Клиент, который получил сообщение</param>
    /// <param name="update">Событие, произошедшее в чате. Новое сообщение, голос в опросе, исключение из чата и т. д.</param>
    /// <param name="cancellationToken">Служебный токен для работы с многопоточностью</param>
    async Task OnMessageReceived(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {     
        var message = update.Message;

        // Получаем ID чата, в которое пришло сообщение. Полезно, чтобы отличать пользователей друг от друга.
        var chatId = message.Chat.Id;

        //проверяет ести ли Id пользователя в словаре состояний бота, если нету, то ну, создаем)
        //позор мой, позор, я так и не разобрался как сделать что то в духе if( status[chatId] == null) из-за возникаюшей ошибки, да и искать решение было лень, сделал вот этот гениальный код, работает))
        try { var z = status[chatId]; }
        catch { status[chatId] = true; }

        // Печатаем на консоль факт получения сообщения
        Console.WriteLine($"Получено сообщение в чате {chatId}: '{message.Date}', '{message.Type}', '{message.Text}', '{message.Audio}'");

        //блок кода, отвечающий за команды в чате
        //если файла нет, то пользователь пишет впервые! даже не нужно обрабатывать /start (по идее)
        if (!System.IO.File.Exists($"{chatId}.txt"))
        {
            System.IO.File.Create($"{chatId}.txt").Close();
            Message sentMessageStart = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: help(),
            cancellationToken: cancellationToken);
            return;
        }
        //помогаем
        if (message.Text == "/help")
        {
            Message sentMessageStart = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: help(),
            cancellationToken: cancellationToken);
            return;
        }
        //украл библиотеку функционал библиотеки, украл список ее команд. Я воровал!!!! 
        if (message.Text == "/info")
        {
            Message sentMessageStart = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: Info(),
            cancellationToken: cancellationToken);
            return;
        }
        //вкл режима диалога
        if (message.Text == "/on")
        {
            status[chatId] = true;
            return;
        }
        //вкл режима беседы
        if (message.Text == "/off")
        {
            status[chatId] = false;
            return;
        }



        //блок кода, отвечающий за игнорирование всех сообщений, кроме выражений в режиме беседы (/off)
        if (!status[chatId] && message.Text is { } && Calculator.Calculate(message.Text).IsValid)
        {
            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: Ansver_(message.Text),
                cancellationToken: cancellationToken); 
        }
        if (!status[chatId]) { return; }
        
        //реализуем основной функционал
        try {
            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: Main(message, chatId),
                cancellationToken: cancellationToken);
        }
        //заносим ошибку в файл с ошибками чтобы понять в чем вообще проблема и на какие данные она выводится + выводим сообщение об ошибке
        catch (Exception ex)
        {
            System.IO.File.AppendAllText($"Report.txt", $"{chatId}: {message.Date}', '{message.Type}', '{message.Text}', '{message.Audio}' \n{ex}");
            Message sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"Что-то пошло не по плану...",
            cancellationToken: cancellationToken);
        }
        
    }
    /// <summary>
    /// Функция, отвечающая почти что за все
    /// определяет чем является сообщение и возвращает строку с какой либо информацией о сообщении
    /// </summary>
    /// <param name="message"></param>
    /// <param name="chatId"></param>
    /// <returns></returns>
    public static string Main(Message message, long chatId)
    {
        //вычисляем
        if (Calculator.Calculate(message.Text).IsValid && message.Text is { } text)
        {
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}', '{message.Text}'\n {Ansver_(text)}");
            return Ansver_(text);
        }
        //обработка мп3 файлов
        if (message.Audio is { } Audio)
        {
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}', '{message.Audio}' \nВаше сообщение весит {Audio.FileSize.ToString()}");
            return $"Ваше сообщение весит {Audio.FileSize.ToString()} байт";
        }
        //обработка стикеров
        if (message.Sticker is { } Sticker)
        {
            //хороший вопрос зачем мне знать тип имя стикера, но мб для шпионажа норм тема) а так юзлес штука, можно опустить
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}', '{message.Sticker.SetName}' \n{Sticker.Emoji}");
            return Sticker.Emoji;

        }
        //обработка текста
        if (message.Text is { } Text)
        {
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}', '{message.Text}', {Text.Length}\nПредложение в длинну {Text.Length} символов");
            return $"Предложение в длину {Text.Length} символов";
        }
        //обработка голосовых сообщений
        if (message.Voice is { } voice)
        {
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}, \nПредложение весит {voice.FileSize}");
            return $"ГС весит {voice.FileSize} байт";
        }
        //обработка фото
        if (message.Photo is { } photo)
        {
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}', '{photo[photo.Length - 1].FileSize}' \nСжатая фотография весит {photo[photo.Length - 1].FileSize}");
            return $"Сжатая фотография весит {photo[photo.Length-1].FileSize} байт";
        }
        //обработка документов
        if (message.Document is { } document)
        {
            System.IO.File.AppendAllText($"{chatId}.txt", $"{message.Date}', '{message.Type}', '{document.FileSize}' \nСжатая фотография весит {document.FileSize}");
            return $"Документ весит {document.FileSize} байт";
        }
        //вывод случайного вариативного ответа, при введении неподдерживаемого файла
        var r = new Random();
        var Answers_on_Not_Correct_data = new string[] { "Ничего не могу с этим поделать.", "Возможно что-то не так со знаками.", "Это как-то связано с математикой? Я не знаю таких выражений.", "Этот файл не поддерживается" };
        var Indx_Answer = r.Next(0, 4);
        //если файл не поддерживается, то запихиваем в файл с ошибками, потому что тенхически это ошибка, да и если будет много запросов на определенный тип файлов, то нужно знать, что на него есть спросс
        System.IO.File.AppendAllText($"Report.txt", $"файл неподдерживается {chatId}: {message.Date}', '{message.Type}', '{message.Text}', '{message.Audio}' \n");
        return Answers_on_Not_Correct_data[Indx_Answer];
    }
    /// <summary>
    /// подаем строку, получаем численный ответ
    /// используется стороння библиотека, а не мои наработки и старания, потому что за мои старания мне не дадут баллов
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static string Ansver_(string message)
    {
            return Calculator.Calculate(message).Result.ToString();
    }
    public static string help()
    {
        return "Вас приветствует бот-калькулятор от Decadence228 " + "\n" +
            "Бот создан в учебных целях!!!" + "\n" +
            "Бот собирает всю информацию о вводах пользователей в txt файлы с их ID, а также собирает все сообщения, приводящие к ошибке, в отдельный файл 'Report.txt'" + "\n" +
            "Функционал: при вводе..." + "\n" +
            "1) корректного математического выражения бот выводит его результат (используется сторонняя библиотека)" + "\n" +
            "2) текста бот выводит его вес в байтах" + "\n" +
            "3) голосового сообщения или mp3 файла бот выводит его вес в байтах" + "\n" +
            "4) стикера я по приколу отвечаю на него эмодзи этого стикера)" + "\n" +
            "5) изображения бот выводит его вес в байтах с учетом сжатия" + "\n" +
            "6) изображения без сжатия бот выводит его вес (работает с любыми документами)" +
            "Функции 2-6 созданы для упрощения ваших вычислений с имеющимися на руках данными," + "\n" +
            "позволяют не лезть в свойства файлов, да и удобно сразу копипастить значения + сразу работать с ними тут, в чате." + "\n" +
            "бот работает в групповых чатах, работает только с сообщениями формата /'*сообщение*' или с любыми сообщениями при выдаче ему прав администратора" + "\n" +
            "/help - выводит вот эту вот табличку)" + "\n" +
            "/info - гайд по калькулятору и его возможностям (украдено)" + "\n" +
            "/on - включить режим диалога (ответ на все сообщения)" + "\n" +
            "/off - выключить режим диалога (ответ ТОЛЬКО на сообщения-выражения (настоятельно рекомендуется в групповых чатах))";
    }
    public static string Info()
    {
        return "FLOOR  expression - Round down to zero accuracy\r\nCEIL  expression - Round up to zero accuracy\r\nABS  expression - Absolute value\r\nROUNDK '(' expression ';' expression ')'" +
            " - Round expr_1 with expr_2 accuracy\r\nROUND  expression - Round with zero accuracy\r\nTRUNC  expression - Trim decimal digits\r\nSIN  expression - Sinus\r\nCOS  expression - Cosinus\r\nTAN " +
            " expression - Tangens\r\nCOT  expression - Cotangens\r\nSINH  expression - Sinus Hypererbolicus\r\nCOSH  expression - Cosinus Hyperbolicus\r\nTANH  expression - Tangens Hyperbolicus\r\nARCSIN  expression" +
            " - Inverse Sinus\r\nARCCOS  expression - Inverse Cosinus\r\nARCTAN  expression - Inverse Tangens\r\nARCTAN2 '(' expression ';' expression ')' - Atan2\r\nARCCOT  expression - Inverse Cotangens\r\nEXP  expression" +
            " - e ^ expr\r\nLN  expression - Logarithm to e\r\nEEX  expression - 10 ^ expr\r\nLOG  expression - Logarithm to 10\r\nRAD  expression - Angle to radians (360° base)\r\nDEG  expression - Radians to angle " +
            "(360° base)\r\nSQRT expression - Square root\r\nSQR expression - Square product\r\nexpression op = ('^'|'**') expression - expr_1 to the expr_2 th power\r\nexpression (MOD | '%' ) expression - Modulo\r\nexpression " +
            "DIV expression - Whole part of division rest\r\nexpression op = ('~'|'//') expression - expr_1 nth root of expr_2\r\nexpression op = ('*'|'/') expression - Multiplication or division\r\nexpression op = ('+'|'-')" +
            " expression - Addition or subtraction\r\nNUMBER- - Single integer or float number\r\nMIN '(' expression (';' expression)* ')' - Minimum\r\nMAX '(' expression (';' expression)* ')' - Maximum\r\nNUMBER- - Single" +
            " integer or float number\r\n'(' expression ')' - Expression within parentheses\r\nPI '()'? - Mathematical constant pi = 3,141593\r\nexpression E+ expression - Exponent, e.g. 10e+43\r\nexpression E- expression " +
            "- Inverted Exponent, e.g. 10e-43\r\nEULER - Mathematical constant e = 2,718282\r\n'-' expression - Unary minus sign (negative numbers)\r\n'+' expression - Unary plus sign (positive numbers)\r\n'(' expression ')'" +
            " expression - Expressions without multiplication sign, e.g. 2(3) -> 2*(3)\r\nexpression '(' expression ')' - Expressions without multiplication sign, e.g. 2(3) -> 2*(3)";
    }
    /// <summary>
    /// Обработчик исключений, возникших при работе бота
    /// </summary>
    /// <param name="botClient">Клиент, для которого возникло исключение</param>
    /// <param name="exception">Возникшее исключение</param>
    /// <param name="cancellationToken">Служебный токен для работы с многопоточностью</param>
    /// <returns></returns>
    Task OnErrorOccured(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // В зависимости от типа исключения печатаем различные сообщения об ошибке
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        
        // Завершаем работу
        return Task.CompletedTask;
    }
}